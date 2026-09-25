using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class CronRunScheduler
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly JobExecutionProgressObserver observer;
    private readonly ConcurrencySettings concurrencySettings;
    private readonly ILogger<CronRunScheduler> logger;

    public CronRunScheduler(
        JobQueueManager jobQueueManager,
        JobRegistry registry,
        TimeProvider timeProvider,
        JobExecutionProgressObserver observer,
        ConcurrencySettings concurrencySettings,
        ILogger<CronRunScheduler> logger)
    {
        this.jobQueueManager = jobQueueManager;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.observer = observer;
        this.concurrencySettings = concurrencySettings;
        this.logger = logger;
    }

    public JobRun? ScheduleNextRun(
        JobDefinition job,
        DateTimeOffset? lastScheduledRunTime = null,
        Action<JobRun>? onRunCreated = null,
        Action<string>? onQueueCreated = null,
        JobRunActivationGate? activationGate = null)
    {
        if (!job.IsEnabled)
        {
            return null;
        }

        var utcNow = timeProvider.GetUtcNow();

        // When rescheduling after a job fires, the timer may have triggered slightly
        // before the scheduled time. Using utcNow directly could return the same cron
        // slot again, causing duplicate execution. Using the later of utcNow and the
        // last scheduled run time guarantees we always advance past the fired slot.
        var nextOccurrenceSearchStart = lastScheduledRunTime > utcNow
            ? lastScheduledRunTime.Value
            : utcNow;
        var nextRunTime = job.GetNextCronOccurrence(nextOccurrenceSearchStart);

        if (!nextRunTime.HasValue)
        {
            return null;
        }

        LogNextJobRun(job.Name, nextRunTime.Value);
        var run = JobRun.CreateCron(
            timeProvider,
            observer.Report,
            job,
            nextRunTime.Value,
            concurrencySettings,
            activationGate);
        onRunCreated?.Invoke(run);
        run.NotifyStateChange(JobStateType.Scheduled);

        // Checked atomically with queue removal, so a job removed concurrently isn't brought back by a pending reschedule.
        if (!jobQueueManager.Enqueue(
                run,
                () => registry.IsRootJob(job),
                onQueueCreated))
        {
            run.NotifyStateChange(JobStateType.Cancelled);
        }

        return run;
    }

    [LoggerMessage(LogLevel.Trace, "Next run of job '{JobName}' is at {NextRun:o}")]
    private partial void LogNextJobRun(string jobName, DateTimeOffset nextRun);
}
