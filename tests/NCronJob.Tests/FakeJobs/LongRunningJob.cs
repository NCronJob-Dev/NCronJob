namespace NCronJob.Tests;

public class LongRunningJob(
    Storage storage,
    TimeProvider timeProvider,
    LongRunningJobSignal? signal = null) : IJob
{
    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        storage.Add($"Running {GetType().Name}");
        signal?.Started.TrySetResult();

        await Task.Delay(TimeSpan.FromHours(1), timeProvider, token);

        throw new InvalidOperationException("I should never be reached");
    }
}

public sealed class LongRunningJobSignal
{
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
