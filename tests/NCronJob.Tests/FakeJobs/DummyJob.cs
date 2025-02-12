namespace NCronJob.Tests;

public class DummyJob(Storage storage) : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        storage.Add($"{GetType().Name} - Parameter: {context.Parameter?.ToString()}");
        return Task.CompletedTask;
    }
}
