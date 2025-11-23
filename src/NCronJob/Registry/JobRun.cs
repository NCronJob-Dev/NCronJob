
using System.Collections.Concurrent;

namespace NCronJob;

internal class JobRun
{
    private readonly JobRun rootJob;
    private int jobExecutionCount;
    private readonly TimeProvider timeProvider;
    private readonly Action<JobRun> progressReporter;
    private readonly ConcurrentBag<JobRun> pendingDependents = [];

    private JobRun(
        TimeProvider timeProvider,
        JobDefinition jobDefinition,
        bool isOneTimeJob,
        DateTimeOffset runAt,
        object? parameter,
        Action<JobRun> progressReporter,
        ITrigger trigger)
    : this(timeProvider, null, jobDefinition, isOneTimeJob, runAt, parameter, progressReporter, trigger)
    { }

    private JobRun(
        TimeProvider timeProvider,
        JobRun? parentJob,
        JobDefinition jobDefinition,
        bool isOneTimeJob,
        DateTimeOffset runAt,
        object? parameter,
        Action<JobRun> progressReporter,
        ITrigger trigger)
    {
        var jobRunId = Guid.NewGuid();

        JobRunId = jobRunId;
        ParentJobRunId = parentJob?.JobRunId;
        IsOrchestrationRoot = parentJob is null;
        CorrelationId = parentJob?.CorrelationId ?? Guid.NewGuid();
        this.timeProvider = timeProvider;
        JobDefinition = jobDefinition;
        RunAt = runAt;
        Parameter = parameter ?? jobDefinition.Parameter;
        Trigger = trigger;

        this.progressReporter = progressReporter;
        rootJob = parentJob is not null ? parentJob.rootJob : this;

        SetState(new JobState(JobStateType.NotStarted, timeProvider.GetUtcNow()));
    }

    internal JobPriority Priority { get; set; } = JobPriority.Normal;

    public Guid JobRunId { get; }
    public Guid? ParentJobRunId { get; }
    public JobDefinition JobDefinition { get; }
    public Guid CorrelationId { get; }
    public bool IsOrchestrationRoot { get; }
    public CancellationToken CancellationToken { get; set; }
    public DateTimeOffset RunAt { get; }

    /// <summary>
    /// At the moment of processing, if the difference between the current time and the scheduled time exceeds the
    /// expiration period (grace period), the job is considered expired and should not be processed. Because the job is not processed,
    /// but it has been dequeued then essentially the job is dropped.
    /// </summary>
    public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(10);
    public bool IsExpired => timeProvider.GetUtcNow() - RunAt > Expiry;
    public bool IsOneTimeJob { get; }
    public object? Parameter { get; }
    public object? ParentOutput { get; set; }
    public ITrigger Trigger { get; }
    public TriggerType TriggerType => Trigger.Type;
    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);

    public static JobRun CreateStartupJob(
        TimeProvider timeProvider,
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition)
    {
        var trigger = new StartupTrigger(jobDefinition.ShouldCrashOnStartupFailure ?? false);
        return new(timeProvider, jobDefinition, true, timeProvider.GetUtcNow(), jobDefinition.Parameter, progressReporter, trigger);
    }

    public static JobRun Create(
        TimeProvider timeProvider,
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition,
        DateTimeOffset runAt)
    {
        if (jobDefinition.CronExpression is null || jobDefinition.UserDefinedCronExpression is null)
        {
            throw new InvalidOperationException("Cannot create a CRON job run without a cron expression.");
        }

        var trigger = new CronTrigger(jobDefinition.CronExpression, jobDefinition.UserDefinedCronExpression, jobDefinition.TimeZone);
        return new(timeProvider, jobDefinition, false, runAt, jobDefinition.Parameter, progressReporter, trigger);
    }

    public static JobRun CreateInstant(
        TimeProvider timeProvider,
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition,
        DateTimeOffset runAt,
        object? parameter,
        CancellationToken token)
    {
        var trigger = new InstantTrigger(runAt);
        return new(timeProvider, jobDefinition, true, runAt, parameter, progressReporter, trigger)
        {
            CancellationToken = token,
        };
    }

    public JobRun CreateDependent(
        JobDefinition jobDefinition,
        object? parameter,
        CancellationToken token)
    {
        var trigger = new DependentTrigger();
        JobRun run = new(timeProvider, this, jobDefinition, true, timeProvider.GetUtcNow(), parameter, progressReporter, trigger)
        {
            CancellationToken = token,
        };

        pendingDependents.Add(run);

        return run;
    }

    public bool RootJobIsCompleted => rootJob.IsCompleted && !rootJob.HasPendingDependentJobs();

    // State change logic
    public bool IsCompleted => CurrentState.IsFinalState();
    public bool CanRun => CurrentState.CanInitiateRun();
    public bool IsCancellable => CurrentState.CanBeCancelled();
    public JobState CurrentState { get; private set; }

    private void SetState(JobState state)
    {
        CurrentState = state;
        progressReporter(this);
    }

    public void NotifyStateChange(JobStateType type, Exception? fault = default)
    {
        if (CurrentState.IsUnchangedAndNotRetrying(type) || CurrentState.IsFinalState())
        {
            return;
        }

        var state = new JobState(type, timeProvider.GetUtcNow(), fault);
        SetState(state);
    }

    public ExecutionProgress ToExecutionProgress()
    {
        return new ExecutionProgress(
            timeProvider.GetUtcNow(),
            CorrelationId,
            MapFrom(CurrentState.Type),
            JobRunId,
            ParentJobRunId,
            JobDefinition.CustomName,
            JobDefinition.Type,
            JobDefinition.IsTypedJob);
    }

    private static ExecutionState MapFrom(JobStateType currentState)
    {
        return currentState switch
        {
            JobStateType.NotStarted => ExecutionState.NotStarted,
            JobStateType.Scheduled => ExecutionState.Scheduled,
            JobStateType.Initializing => ExecutionState.Initializing,
            JobStateType.Running => ExecutionState.Running,
            JobStateType.Retrying => ExecutionState.Retrying,
            JobStateType.Completing => ExecutionState.Completing,
            JobStateType.WaitingForDependency => ExecutionState.WaitingForDependency,
            JobStateType.Skipped => ExecutionState.Skipped,
            JobStateType.Completed => ExecutionState.Completed,
            JobStateType.Faulted => ExecutionState.Faulted,
            JobStateType.Cancelled => ExecutionState.Cancelled,
            JobStateType.Expired => ExecutionState.Expired,
            _ => ExecutionState.Undetermined,
        };
    }

    private bool HasPendingDependentJobs()
    {
        return !pendingDependents.IsEmpty && pendingDependents.Any(j => !j.IsCompleted || j.HasPendingDependentJobs());
    }
}
