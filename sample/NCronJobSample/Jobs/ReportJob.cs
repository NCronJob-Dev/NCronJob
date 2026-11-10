using NCronJob;

namespace NCronJobSample;

public partial class ReportJob(ILogger<ReportJob> logger) : IJob<ReportRequest>
{
    public Task RunAsync(IJobExecutionContext<ReportRequest> context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogReport(context.Parameter.Region, context.Parameter.Recipients.Count, context.Parameter.Options.Format);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Sending the {Region} report as {Format} to {RecipientCount} recipients.")]
    private partial void LogReport(string region, int recipientCount, string format);
}
