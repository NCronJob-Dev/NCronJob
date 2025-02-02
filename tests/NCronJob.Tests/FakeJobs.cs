namespace NCronJob.Tests;

public sealed class JobThatThrowsInCtor : IJob
{
    public JobThatThrowsInCtor()
        => throw new InvalidOperationException();

    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        => Task.CompletedTask;
}
