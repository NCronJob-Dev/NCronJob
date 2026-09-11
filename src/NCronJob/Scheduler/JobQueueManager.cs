using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal sealed class JobQueueManager : IDisposable
{
    private readonly ConcurrentDictionary<string, JobQueue> jobQueues = new();
    private readonly Dictionary<string, TaskCompletionSource> queueSignals = [];
#if NET9_0_OR_GREATER
    private readonly Lock syncLock = new();
#else
    private readonly object syncLock = new();
#endif

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event Action<string>? QueueAdded;

    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Adds the run to its queue, creating the queue if needed.
    /// Lookup and enqueue are atomic with respect to <see cref="RemoveQueue"/>, so a run can never end up in a removed queue.
    /// </summary>
    /// <returns><c>false</c> when <paramref name="canEnqueue"/> rejected the run.</returns>
    public bool Enqueue(JobRun run, Func<bool>? canEnqueue = null)
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
                queueSignals[jt] = CreateSignal();
                return queue;
            });

            jobQueue.EnqueueForDirectExecution(run);
        }

        if (isCreating)
        {
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
            jobQueue.CollectionChanged -= CallCollectionChanged;

            if (queueSignals.Remove(queueName, out var signal))
            {
                signal.TrySetResult();
            }
        }

        // Progress callbacks run user code, so they must not be invoked while holding the lock.
        foreach (var run in cancellableRuns)
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
            return queueSignals.TryGetValue(queueName, out var signal) ? signal.Task : Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        lock (syncLock)
        {
            foreach (var jobQueue in jobQueues.Values)
            {
                jobQueue.CollectionChanged -= CallCollectionChanged;
            }

            foreach (var signal in queueSignals.Values)
            {
                signal.TrySetResult();
            }

            jobQueues.Clear();
            queueSignals.Clear();

            IsDisposed = true;
        }
    }

    private void SignalJobQueue(string queueName)
    {
        lock (syncLock)
        {
            if (!queueSignals.TryGetValue(queueName, out var signal))
            {
                return;
            }

            queueSignals[queueName] = CreateSignal();
            signal.TrySetResult();
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

    private static TaskCompletionSource CreateSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
