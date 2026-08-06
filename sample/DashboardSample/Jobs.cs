using NCronJob;

namespace DashboardSample;

internal sealed record PipelineOptions(string Region, int BatchSize);

internal sealed class PipelineJob(ILogger<PipelineJob> logger) : IJob
{
    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Processing pipeline {CorrelationId}", context.CorrelationId);
        }
        await Task.Delay(TimeSpan.FromSeconds(3), token);
    }
}

internal sealed class ArchiveJob(ILogger<ArchiveJob> logger) : IJob
{
    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation("Archiving pipeline output");
        await Task.Delay(TimeSpan.FromSeconds(2), token);
    }
}

internal sealed class AlertJob(ILogger<AlertJob> logger) : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogWarning("Pipeline failure alert");
        return Task.CompletedTask;
    }
}
