namespace NCronJob.Tests;

public sealed class ExceptionJob : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        => throw new InvalidOperationException();
}
