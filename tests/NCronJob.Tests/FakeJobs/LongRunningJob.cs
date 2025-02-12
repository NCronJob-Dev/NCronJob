namespace NCronJob.Tests;

public class LongRunningJob(Storage storage, TimeProvider timeProvider) : IJob
{
    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        storage.Add($"Running {GetType().Name}");

        await Task.Delay(TimeSpan.FromHours(1), timeProvider, token);

        throw new InvalidOperationException("I should never be reached");
    }
}
