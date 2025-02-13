namespace NCronJob.Tests;

public class DummyJob(Storage storage) : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        storage.Add(this.GetType().Name);
        return Task.CompletedTask;
    }
}
