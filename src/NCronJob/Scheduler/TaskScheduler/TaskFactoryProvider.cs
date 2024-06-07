namespace NCronJob;

internal static class TaskFactoryProvider
{
    private static readonly TaskFactory TaskFactory = new(
        CancellationToken.None,
        TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness,
        TaskContinuationOptions.None,
        TaskScheduler.Default //new DeterministicTaskScheduler()
    );

    public static TaskFactory GetTaskFactory() => TaskFactory;
}
