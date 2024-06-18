namespace NCronJob;

internal static class TaskFactoryProvider
{
    private static TaskScheduler TaskScheduler => ConcurrencySettings.UseDeterministicTaskScheduler
        ? new DeterministicTaskScheduler()
        : TaskScheduler.Default;

    private static readonly TaskFactory TaskFactory = new(
        CancellationToken.None,
        TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness,
        TaskContinuationOptions.None,
        TaskScheduler
    );

    public static TaskFactory GetTaskFactory() => TaskFactory;
}
