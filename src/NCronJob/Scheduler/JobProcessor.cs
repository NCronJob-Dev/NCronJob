using Microsoft.Extensions.Logging;

namespace NCronJob;
internal sealed partial class JobProcessor
{
    private readonly JobExecutor jobExecutor;
    private readonly ILogger<JobProcessor> logger;

    public JobProcessor(JobExecutor jobExecutor, ILogger<JobProcessor> logger)
    {
        this.jobExecutor = jobExecutor;
        this.logger = logger;
    }

    public async Task ProcessJobAsync(JobRun jobRun, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            if (jobRun.IsExpired)
            {
                LogDequeuingExpiredJob(jobRun.JobDefinition.JobName);
                jobRun.JobDefinition.NotifyStateChange(new JobState(JobStateType.Expired));
                return;
            }

            jobRun.JobDefinition.NotifyStateChange(new JobState(JobStateType.Running));

            await jobExecutor.RunJob(jobRun, cancellationToken).ConfigureAwait(false);

            jobRun.JobDefinition.NotifyStateChange(new JobState(JobStateType.Completed));
        }
        catch (Exception ex)
        {
            jobRun.JobDefinition.NotifyStateChange(new JobState(JobStateType.Failed, ex.Message));
        }
        finally
        {
            semaphore.Release();
        }
    }

    [LoggerMessage(LogLevel.Trace, "Dequeuing job {JobName} because it has exceeded the expiration period.")]
    private partial void LogDequeuingExpiredJob(string jobName);
}
