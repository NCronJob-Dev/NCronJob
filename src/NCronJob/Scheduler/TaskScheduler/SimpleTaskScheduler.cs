using System.Collections.Concurrent;

namespace NCronJob;

/// <summary>
/// Simple, high-performance scheduling: Use when tasks are independent, short-lived, and can be executed immediately without complex scheduling requirements.
/// </summary>
internal class SimpleTaskScheduler : TaskScheduler
{
    private readonly ConcurrentQueue<Task> tasks = new();

    protected override IEnumerable<Task> GetScheduledTasks() =>  [..tasks];

    protected override void QueueTask(Task task)
    {
        tasks.Enqueue(task);
        ThreadPool.QueueUserWorkItem(_ => TryExecuteTask(task));
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) =>
        taskWasPreviouslyQueued ? TryDequeue(task) && TryExecuteTask(task) : TryExecuteTask(task);

    protected override bool TryDequeue(Task task) => tasks.TryDequeue(out _);
}
