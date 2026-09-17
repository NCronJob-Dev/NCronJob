
using System.Collections.Concurrent;

namespace NCronJob;

internal class JobRun
{
    private readonly JobRun rootJob;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrencySettings settings;
    private readonly Action<JobRun> progressReporter;
    private readonly JobRunActivationGate? activationGate;
    private readonly ConcurrentBag<JobRun> pendingDependents = [];

    private JobRun(
        TimeProvider timeProvider,
        JobDefinition jobDefinition,
        DateTimeOffset runAt,
        OptionalParameter parameter,
        Action<JobRun> progressReporter,
        TriggerType triggerType,
        ConcurrencySettings settings,
        JobRunActivationGate? activationGate = null)
    : this(timeProvider, null, jobDefinition, runAt, parameter, progressReporter, triggerType, settings, activationGate)
    {
    }

    private JobRun(
        TimeProvider timeProvider,
        JobRun? parentJob,
        JobDefinition jobDefinition,
        DateTimeOffset runAt,
        OptionalParameter parameter,
        Action<JobRun> progressReporter,
        TriggerType triggerType,
        ConcurrencySettings settings,
        JobRunActivationGate? activationGate = null)
    {
        var jobRunId = Guid.NewGuid();

        JobRunId = jobRunId;
        ParentJobRunId = parentJob?.JobRunId;
        IsOrchestrationRoot = parentJob is null;
        CorrelationId = parentJob?.CorrelationId ?? Guid.NewGuid();
        this.timeProvider = timeProvider;
        this.settings = settings;
        this.activationGate = activationGate;
        JobDefinition = jobDefinition;
        RunAt = runAt;
        Parameter = parameter.IsSpecified ? parameter.Value : jobDefinition.Parameter;
        TriggerType = triggerType;

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
    public TimeSpan Expiry => JobDefinition.JobRunExpiry ?? settings.DefaultJobRunExpiry;
    public bool IsExpired => Expiry != Timeout.InfiniteTimeSpan && timeProvider.GetUtcNow() - RunAt > Expiry;
    public object? Parameter { get; }
    public object? ParentOutput { get; set; }
    public TriggerType TriggerType { get; }

    public static JobRun CreateStartupJob(
        TimeProvider timeProvider,
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition,
        ConcurrencySettings? settings = null)
    => new(timeProvider, jobDefinition, timeProvider.GetUtcNow(), OptionalParameter.Unspecified, progressReporter, TriggerType.Startup, settings ?? new ConcurrencySettings());

    public static JobRun Create(
        TimeProvider timeProvider,
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition,
        DateTimeOffset runAt,
        ConcurrencySettings? settings = null,
        JobRunActivationGate? activationGate = null)
    => new(
        timeProvider,
        jobDefinition,
        runAt,
        OptionalParameter.Unspecified,
        progressReporter,
        TriggerType.Cron,
        settings ?? new ConcurrencySettings(),
        activationGate);

    public static JobRun CreateInstant(
        TimeProvider timeProvider,
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition,
        DateTimeOffset runAt,
        OptionalParameter parameter,
        CancellationToken token,
        ConcurrencySettings? settings = null)
    => new(timeProvider, jobDefinition, runAt, parameter, progressReporter, TriggerType.Instant, settings ?? new ConcurrencySettings())
    {
        CancellationToken = token,
    };

    public JobRun CreateDependent(
        JobDefinition jobDefinition,
        object? parameter,
        CancellationToken token)
    {
        JobRun run = new(
            timeProvider,
            this,
            jobDefinition,
            timeProvider.GetUtcNow(),
            parameter is null ? OptionalParameter.Unspecified : OptionalParameter.FromValue(parameter),
            progressReporter,
            TriggerType.Dependent,
            settings)
        {
            CancellationToken = token,
        };

        pendingDependents.Add(run);

        return run;
    }

    public bool RootJobIsCompleted => rootJob.IsCompleted && !rootJob.HasPendingDependentJobs();

    public ValueTask<bool> WaitForActivationAsync() =>
        activationGate is null
            ? ValueTask.FromResult(true)
            : new ValueTask<bool>(activationGate.WaitAsync());

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

internal sealed class JobRunActivationGate
{
    private readonly TaskCompletionSource<bool> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<bool> WaitAsync() => completion.Task;

    public void Activate() => completion.TrySetResult(true);

    public void Reject() => completion.TrySetResult(false);
}

internal readonly record struct OptionalParameter(bool IsSpecified, object? Value)
{
    public static OptionalParameter Unspecified => default;

    public static OptionalParameter FromValue(object? value) => new(true, value);
}
