using Microsoft.Extensions.Logging;

namespace NCronJob;

#pragma warning disable CA2008
internal sealed partial class JobProcessor
{
    private readonly JobExecutor jobExecutor;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<JobProcessor> logger;
    private readonly TaskFactory taskFactory;

    public JobProcessor(
        JobExecutor jobExecutor,
        TimeProvider timeProvider,
        ILogger<JobProcessor> logger)
    {
        this.jobExecutor = jobExecutor;
        this.timeProvider = timeProvider;
        this.logger = logger;
        
        taskFactory = TaskFactoryProvider.GetTaskFactory();
    }

    public async Task ProcessJobAsync(JobRun jobRun, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            if (jobRun.IsExpired(timeProvider))
            {
                LogDequeuingExpiredJob(jobRun.JobDefinition.JobName);
                jobRun.JobDefinition.NotifyStateChange(new JobState(JobStateType.Expired));
                return;
            }

            jobRun.JobDefinition.NotifyStateChange(new JobState(JobStateType.Running));

            await taskFactory.StartNew(() => jobExecutor.RunJob(jobRun, cancellationToken), cancellationToken)
                .Unwrap()
                .ConfigureAwait(false);

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
