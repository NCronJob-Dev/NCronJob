namespace NCronJob.Tests;

public class DummyJob(Storage storage) : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Implementation(GetType().Name, storage, context, token);
        return Task.CompletedTask;
    }

    internal static void Implementation(
        string name,
        Storage storage,
        IJobExecutionContext context,
        CancellationToken token)
    {
        storage.Add($"{name} - Parameter: {context.Parameter?.ToString()}");
        context.Output = UpdateWith(context);
    }

    private static object? UpdateWith(IJobExecutionContext context)
    {
        if (context.Parameter is null)
        {
            return context.Output;
        }

        return $"{context.Output}+{context.Parameter}";
    }
}
