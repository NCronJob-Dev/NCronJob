using System.Collections.Concurrent;

namespace NCronJob;

internal class NCronJobTaskScheduler : TaskScheduler
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
