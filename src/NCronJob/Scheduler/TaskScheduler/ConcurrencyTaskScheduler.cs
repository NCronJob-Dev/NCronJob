using System.Collections.Concurrent;

namespace NCronJob;

/// <summary>
/// Controlled parallelism and balanced concurrency: Use when you need to manage the maximum degree of parallelism explicitly
/// and ensure tasks are processed with a controlled level of concurrency.
/// </summary>
internal class ConcurrencyTaskScheduler : TaskScheduler
{
    [ThreadStatic]
    private static bool currentThreadIsProcessingItems;

    private readonly ConcurrentQueue<Task> tasks = new();
    private readonly int maxDegreeOfParallelism;
    private int delegatesQueuedOrRunning;

    public sealed override int MaximumConcurrencyLevel => maxDegreeOfParallelism;
    
    public ConcurrencyTaskScheduler(int maxDegreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);

        this.maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    protected sealed override IEnumerable<Task> GetScheduledTasks() => [.. tasks];

    protected sealed override void QueueTask(Task task)
    {
        tasks.Enqueue(task);
        if (Interlocked.CompareExchange(ref delegatesQueuedOrRunning, maxDegreeOfParallelism, maxDegreeOfParallelism) < maxDegreeOfParallelism)
        {
            Interlocked.Increment(ref delegatesQueuedOrRunning);
            NotifyThreadPoolOfPendingWork();
        }
    }

#pragma warning disable CA2008
    private void NotifyThreadPoolOfPendingWork() =>
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            currentThreadIsProcessingItems = true;

            try
            {
                while (tasks.TryDequeue(out var task))
                {
                    TryExecuteTask(task);
                }
            }
            finally
            {
                currentThreadIsProcessingItems = false;
                Interlocked.Decrement(ref delegatesQueuedOrRunning);
                if (!tasks.IsEmpty)
                {
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }, null);
#pragma warning restore CA2008

    protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) =>
        currentThreadIsProcessingItems &&
        (taskWasPreviouslyQueued ? TryDequeue(task) && TryExecuteTask(task) : TryExecuteTask(task));

    protected sealed override bool TryDequeue(Task task) => tasks.TryDequeue(out _);

    
}
