namespace NCronJob;

/// <summary>
/// Deterministic and ordered execution: Use when the order of task execution is critical, and tasks may have dependencies that require strict ordering.
/// </summary>
internal class DeterministicTaskScheduler : TaskScheduler
{
    private readonly LinkedList<Task> tasks = [];
    private readonly object @lock = new();

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        lock (@lock)
        {
            return [..tasks];
        }
    }

    protected override void QueueTask(Task task)
    {
        lock (@lock)
        {
            tasks.AddLast(task);
            ThreadPool.UnsafeQueueUserWorkItem(_ => TryExecuteTask(task), null);
        }
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
        lock (@lock)
        {
            return tasks.Remove(task);
        }
    }
}
