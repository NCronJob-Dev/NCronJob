using NCronJob;

namespace NCronJobSample;

public partial class WarmupCacheJob(ILogger<WarmupCacheJob> logger) : IJob
{
    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        await Task.Delay(1500, token);
        LogWarmedUp(logger);
    }

    [LoggerMessage(LogLevel.Information, "Cache warmed up.")]
    private static partial void LogWarmedUp(ILogger logger);
}
