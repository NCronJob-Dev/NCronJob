using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal sealed class JobQueueManager : IDisposable
{
    private readonly ConcurrentDictionary<string, JobQueue> jobQueues = new();
    private readonly Dictionary<string, AsyncSignal> queueSignals = [];
    private readonly SyncLock syncLock = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event Action<string>? QueueAdded;

    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Adds the run to its queue, creating the queue if needed.
    /// Lookup and enqueue are atomic with respect to <see cref="RemoveQueue"/>, so a run can never end up in a removed queue.
    /// </summary>
    /// <returns><c>false</c> when <paramref name="canEnqueue"/> rejected the run.</returns>
    public bool Enqueue(
        JobRun run,
        Func<bool>? canEnqueue = null,
        Action<string>? onQueueCreated = null)
    {
        var queueName = run.JobDefinition.JobFullName;
        var isCreating = false;

        lock (syncLock)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (canEnqueue is not null && !canEnqueue())
            {
                return false;
            }

            var jobQueue = jobQueues.GetOrAdd(queueName, jt =>
            {
                isCreating = true;
                var queue = new JobQueue(jt);
                queue.CollectionChanged += CallCollectionChanged;
                queueSignals[jt] = new AsyncSignal();
                return queue;
            });

            jobQueue.EnqueueForDirectExecution(run);
        }

        if (isCreating)
        {
            onQueueCreated?.Invoke(queueName);
            QueueAdded?.Invoke(queueName);
        }

        return true;
    }

    public void RemoveQueue(string queueName)
    {
        List<JobRun> cancellableRuns;

        lock (syncLock)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (!jobQueues.TryRemove(queueName, out var jobQueue))
            {
                return;
            }

            cancellableRuns = jobQueue.Where(j => j.IsCancellable).ToList();

            jobQueue.Clear();
            DetachQueueUnsafe(queueName, jobQueue);
        }

        // Progress callbacks run user code, so they must not be invoked while holding the lock.
        foreach (var run in cancellableRuns)
        {
            run.NotifyStateChange(JobStateType.Cancelled);
        }
    }

    public void RemoveRuns(
        IReadOnlyCollection<JobRun> runs,
        IReadOnlyCollection<string>? createdQueueNames = null)
    {
        if (runs.Count == 0 && createdQueueNames is not { Count: > 0 })
        {
            return;
        }

        var runSet = new HashSet<JobRun>(runs, ReferenceEqualityComparer.Instance);
        var createdQueueSet = createdQueueNames is null
            ? []
            : new HashSet<string>(createdQueueNames, StringComparer.Ordinal);
        lock (syncLock)
        {
            if (!IsDisposed)
            {
                foreach (var (queueName, jobQueue) in jobQueues.ToArray())
                {
                    var removedRuns = jobQueue.RemoveWhere(runSet.Contains);
                    var removeEmptyCreatedQueue = createdQueueSet.Contains(queueName) && jobQueue.Count == 0;

                    if (removedRuns.Count == 0 && !removeEmptyCreatedQueue)
                    {
                        continue;
                    }

                    if (jobQueue.Count == 0)
                    {
                        jobQueues.TryRemove(queueName, out _);
                        DetachQueueUnsafe(queueName, jobQueue);
                    }
                    else
                    {
                        SignalJobQueueUnsafe(queueName);
                    }
                }
            }
        }

        foreach (var run in runs.Where(run => run.IsCancellable))
        {
            run.NotifyStateChange(JobStateType.Cancelled);
        }
    }

    public bool TryGetQueue(string queueName, [MaybeNullWhen(false)] out JobQueue jobQueue)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return jobQueues.TryGetValue(queueName, out jobQueue);
    }

    public IEnumerable<string> GetAllJobQueueNames()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return jobQueues.Keys;
    }

    /// <summary>
    /// Returns a task that completes the next time the given queue changes or is removed.
    /// Obtain it before inspecting the queue so that no change can be missed.
    /// </summary>
    public Task WaitForChangeAsync(string queueName)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        lock (syncLock)
        {
            return queueSignals.TryGetValue(queueName, out var signal) ? signal.WaitAsync() : Task.CompletedTask;
        }
    }

    internal async Task WaitUntilEmptyAsync(string queueName, CancellationToken cancellationToken)
    {
        while (true)
        {
            var dequeued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
            {
                if (sender is JobQueue queue
                    && queue.Name == queueName
                    && args.Action == NotifyCollectionChangedAction.Remove)
                {
                    dequeued.TrySetResult();
                }
            }

            CollectionChanged += OnCollectionChanged;
            try
            {
                Task queueChanged;
                lock (syncLock)
                {
                    ObjectDisposedException.ThrowIf(IsDisposed, this);

                    if (!jobQueues.TryGetValue(queueName, out var queue) || queue.Count == 0)
                    {
                        return;
                    }

                    queueChanged = queueSignals[queueName].WaitAsync();
                }

                await Task.WhenAny(dequeued.Task, queueChanged)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                CollectionChanged -= OnCollectionChanged;
            }
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        lock (syncLock)
        {
            foreach (var (queueName, jobQueue) in jobQueues)
            {
                DetachQueueUnsafe(queueName, jobQueue);
            }

            jobQueues.Clear();

            IsDisposed = true;
        }
    }

    private void SignalJobQueue(string queueName)
    {
        lock (syncLock)
        {
            SignalJobQueueUnsafe(queueName);
        }
    }

    private void SignalJobQueueUnsafe(string queueName)
    {
        if (queueSignals.TryGetValue(queueName, out var signal))
        {
            signal.Pulse();
        }
    }

    private void DetachQueueUnsafe(string queueName, JobQueue jobQueue)
    {
        jobQueue.CollectionChanged -= CallCollectionChanged;

        if (queueSignals.Remove(queueName, out var signal))
        {
            signal.Complete();
        }
    }

    private void CallCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is JobQueue jobQueue && e.Action == NotifyCollectionChangedAction.Add)
        {
            SignalJobQueue(jobQueue.Name);
        }

        CollectionChanged?.Invoke(sender, e);
    }
}
