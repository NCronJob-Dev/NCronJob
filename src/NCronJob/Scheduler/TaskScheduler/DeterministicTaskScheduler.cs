using System.Collections.Concurrent;

namespace NCronJob;

/// <summary>
/// Deterministic and ordered execution: Use when the order of task execution is critical, and tasks may have dependencies that require strict ordering.
/// </summary>
internal class DeterministicTaskScheduler : TaskScheduler
{
    private readonly ConcurrentQueue<Task> tasks = new();

    protected override IEnumerable<Task> GetScheduledTasks() => [.. tasks];

    protected override void QueueTask(Task task)
    {
        tasks.Enqueue(task);
        ThreadPool.UnsafeQueueUserWorkItem(_ => TryExecuteTask(task), null);
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (taskWasPreviouslyQueued)
        {
            TryDequeue(task);
        }

        return TryExecuteTask(task);
    }

    protected override bool TryDequeue(Task task)
    {
        tasks.TryDequeue(out var dequeuedTask);
        return dequeuedTask == task;
    }
}
