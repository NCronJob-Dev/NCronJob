using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobProcessor
{
    private readonly JobExecutor jobExecutor;
    private readonly ILogger<JobProcessor> logger;

    public JobProcessor(
        JobExecutor jobExecutor,
        ILogger<JobProcessor> logger)
    {
        this.jobExecutor = jobExecutor;
        this.logger = logger;
    }

    public async Task ProcessJobAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        try
        {
            if (jobRun.IsExpired)
            {
                LogDequeuingExpiredJob(jobRun.JobDefinition.JobName);
                jobRun.NotifyStateChange(JobStateType.Expired);
                return;
            }

            jobRun.NotifyStateChange(JobStateType.Initializing);

            await jobExecutor.RunJob(jobRun, cancellationToken).ConfigureAwait(false);

            jobRun.NotifyStateChange(JobStateType.Completed);
        }
        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested || oce.CancellationToken.IsCancellationRequested)
        {
            jobRun.NotifyStateChange(JobStateType.Cancelled);
        }
        catch (Exception ex)
        {
            jobRun.NotifyStateChange(JobStateType.Faulted, ex);
        }
        finally
        {
            jobRun.IncrementJobExecutionCount();
        }
    }

    [LoggerMessage(LogLevel.Trace, "Dequeuing job {JobName} because it has exceeded the expiration period.")]
    private partial void LogDequeuingExpiredJob(string jobName);
}
