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
    private bool disposed;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event Action<string>? QueueAdded;

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
        if (jobQueues.TryRemove(queueName, out var jobQueue))
        {
            jobQueue.Clear();
            jobQueue.CollectionChanged -= CallCollectionChanged;

            if (semaphores.TryRemove(queueName, out var semaphore))
            {
                semaphore.Dispose();
            }

            if (jobCancellationTokens.TryRemove(queueName, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    public bool TryGetQueue(string queueName, [MaybeNullWhen(false)] out JobQueue jobQueue) => jobQueues.TryGetValue(queueName, out jobQueue);

    public IEnumerable<string> GetAllJobQueueNames() => jobQueues.Keys;

    public SemaphoreSlim GetOrAddSemaphore(string jobType, int concurrencyLimit) =>
        semaphores.GetOrAdd(jobType, _ => new SemaphoreSlim(concurrencyLimit));

    public CancellationTokenSource GetOrAddCancellationTokenSource(string queueName) =>
        jobCancellationTokens.AddOrUpdate(queueName,
            _ => new CancellationTokenSource(),
            (_, existingCts) =>
            {
                if (existingCts.IsCancellationRequested)
                {
                    existingCts.Dispose();
                    return new CancellationTokenSource();
                }
                return existingCts;
            });

    public void SignalJobQueue(string queueName)
    {
        if (jobCancellationTokens.TryGetValue(queueName, out var cts))
        {
            cts.Cancel();
            Task.Delay(10).GetAwaiter().GetResult();
            jobCancellationTokens[queueName] = new CancellationTokenSource();
        }
    }

    public int Count(string queueName) => jobQueues.TryGetValue(queueName, out var jobQueue) ? jobQueue.Count : 0;

    public void Dispose() => Dispose(true);

    private void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
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
            jobCancellationTokens.Clear();

            jobQueues.Clear();
            semaphores.Clear();
        }

        disposed = true;
    }

    private void CallCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var handler = Interlocked.CompareExchange(ref CollectionChanged, null, null);
        handler?.Invoke(sender, e);
    }
}
