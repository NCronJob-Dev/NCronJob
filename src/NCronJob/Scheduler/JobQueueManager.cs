using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal sealed class JobQueueManager : IDisposable
{
    private readonly ConcurrentDictionary<string, JobQueue> jobQueues = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> semaphores = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> jobCancellationTokens = new();
#if NET9_0_OR_GREATER
    private readonly Lock syncLock = new();
#else
    private readonly object syncLock = new();
#endif

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event Action<string>? QueueAdded;

    public bool IsDisposed { get; private set; }

    public JobQueue GetOrAddQueue(string queueName)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        lock (syncLock)
        {
            var isCreating = false;
            var jobQueue = jobQueues.GetOrAdd(queueName, jt =>
            {
                isCreating = true;
                var queue = new JobQueue(jt);
                queue.CollectionChanged += CallCollectionChanged;
                jobCancellationTokens[jt] = new CancellationTokenSource();
                return queue;
            });

            if (isCreating)
            {
                QueueAdded?.Invoke(queueName);
            }

            return jobQueue;
        }
    }

    public void RemoveQueue(string queueName)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        lock (syncLock)
        {
            if (jobQueues.TryRemove(queueName, out var jobQueue))
            {
                foreach (var job in jobQueue.Where(j => j.IsCancellable))
                {
                    job.NotifyStateChange(JobStateType.Cancelled);
                }

                jobQueue.Clear();
                jobQueue.CollectionChanged -= CallCollectionChanged;
                semaphores.TryRemove(queueName, out _);
                if (jobCancellationTokens.TryRemove(queueName, out CancellationTokenSource? x))
                {
                    x.Cancel();
                    x.Dispose();
                }
            }
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

    public SemaphoreSlim GetOrAddSemaphore(string queueName, int concurrencyLimit)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return semaphores.GetOrAdd(queueName, _ => new SemaphoreSlim(concurrencyLimit));
    }

    public CancellationTokenSource GetCancellationTokenSource(string queueName)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return jobCancellationTokens[queueName];
    }

    public void SignalJobQueue(string queueName)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        lock (syncLock)
        {
            if (!jobQueues.ContainsKey(queueName))
            {
                return;
            }

            var cts = jobCancellationTokens[queueName];
            cts.Cancel();
            jobCancellationTokens[queueName] = new CancellationTokenSource();
        }
    }

    public int Count(string queueName) => jobQueues.TryGetValue(queueName, out var jobQueue) ? jobQueue.Count : 0;

    public void Dispose()
    {
        if (IsDisposed)
            return;

        foreach (var jobQueue in jobQueues.Values)
        {
            jobQueue.CollectionChanged -= CallCollectionChanged;
        }

        foreach (var semaphore in semaphores.Values)
        {
            semaphore.Dispose();
        }

        foreach (var cts in jobCancellationTokens.Values)
        {
            cts.Dispose();
        }

        jobQueues.Clear();
        semaphores.Clear();
        jobCancellationTokens.Clear();

        IsDisposed = true;
    }

    private void CallCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => CollectionChanged?.Invoke(sender, e);
}
