namespace NCronJob.Tests;

public class DummyJob(Storage storage) : IJob<object?>
{
    public Task RunAsync(IJobExecutionContext<object?> context, CancellationToken token)
    {
        storage.Add($"{GetType().Name} - Parameter: {context.Parameter?.ToString()}");
        context.Output = UpdateWith(context);
        return Task.CompletedTask;
    }

    private static object? UpdateWith(IJobExecutionContext<object?> context)
    {
        return context.Parameter is null ? context.Output : $"{context.Output}+{context.Parameter}";
    }
}
