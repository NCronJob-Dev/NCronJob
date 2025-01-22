using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal sealed class JobQueueManager : IDisposable
{
    private readonly TimeProvider timeProvider;
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

    public JobQueueManager(TimeProvider timeProvider) => this.timeProvider = timeProvider;

    public JobQueue GetOrAddQueue(string queueName)
    {
        var isCreating = false;
        var jobQueue = jobQueues.GetOrAdd(queueName, jt =>
        {
            isCreating = true;
            var queue = new JobQueue(timeProvider, jt);
            queue.CollectionChanged += CallCollectionChanged;
            return queue;
        });

        if (isCreating)
        {
            QueueAdded?.Invoke(queueName);
        }

        return jobQueue;
    }

    public void RemoveQueue(string queueName)
    {
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
                semaphores.Clear();
                jobCancellationTokens.Clear();
            }
        }
    }

    public bool TryGetQueue(string queueName, [MaybeNullWhen(false)] out JobQueue jobQueue) => jobQueues.TryGetValue(queueName, out jobQueue);

    public IEnumerable<string> GetAllJobQueueNames() => jobQueues.Keys;

    public SemaphoreSlim GetOrAddSemaphore(string jobType, int concurrencyLimit) =>
        semaphores.GetOrAdd(jobType, _ => new SemaphoreSlim(concurrencyLimit));

    public CancellationTokenSource GetOrAddCancellationTokenSource(string queueName)
    {
        lock (syncLock)
        {
            if (jobCancellationTokens.TryGetValue(queueName, out var cts))
            {
                if (cts.IsCancellationRequested)
                {
                    cts.Dispose();
                    jobCancellationTokens[queueName] = new CancellationTokenSource();
                }
            }
            else
            {
                jobCancellationTokens[queueName] = new CancellationTokenSource();
            }

            return jobCancellationTokens[queueName];
        }
    }

    public void SignalJobQueue(string queueName)
    {
        lock (syncLock)
        {
            if (jobCancellationTokens.TryGetValue(queueName, out var cts))
            {
                cts.Cancel();
                Task.Delay(10).GetAwaiter().GetResult();
                jobCancellationTokens[queueName] = new CancellationTokenSource();
            }
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
