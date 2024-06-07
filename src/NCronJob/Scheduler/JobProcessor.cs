using Microsoft.Extensions.Logging;

namespace NCronJob;

#pragma warning disable CA2008
internal sealed partial class JobProcessor
{
    private readonly JobExecutor jobExecutor;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<JobProcessor> logger;

    public JobProcessor(
        JobExecutor jobExecutor,
        TimeProvider timeProvider,
        ILogger<JobProcessor> logger)
    {
        this.jobExecutor = jobExecutor;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task ProcessJobAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        try
        {
            if (jobRun.IsExpired(timeProvider))
            {
                LogDequeuingExpiredJob(jobRun.JobDefinition.JobName);
                jobRun.NotifyStateChange(JobStateType.Expired);
                return;
            }

            jobRun.NotifyStateChange(JobStateType.Running);

            await jobExecutor.RunJob(jobRun, cancellationToken).ConfigureAwait(false);

            jobRun.NotifyStateChange(JobStateType.Completed);
        }
        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested || oce.CancellationToken.IsCancellationRequested)
        {
            jobRun.NotifyStateChange(JobStateType.Cancelled);
        }
        catch (Exception ex)
        {
            jobRun.NotifyStateChange(JobStateType.Faulted, ex.Message);
        }
        finally
        {
            jobRun.IncrementJobExecutionCount();
        }
    }

    [LoggerMessage(LogLevel.Trace, "Dequeuing job {JobName} because it has exceeded the expiration period.")]
    private partial void LogDequeuingExpiredJob(string jobName);
}
