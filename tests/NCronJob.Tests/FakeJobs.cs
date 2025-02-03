namespace NCronJob.Tests;

public sealed class JobThatThrowsInCtor : IJob
{
    public JobThatThrowsInCtor()
        => throw new InvalidOperationException();

    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        => Task.CompletedTask;
}

public sealed class ExceptionJob : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        => throw new InvalidOperationException();
}
