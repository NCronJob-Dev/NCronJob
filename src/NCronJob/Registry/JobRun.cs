
using System.Collections.Concurrent;

namespace NCronJob;

internal class JobRun
{
    private readonly JobRun rootJob;
    private int jobExecutionCount;
    private readonly Action<JobRun> progressReporter;
    private readonly ConcurrentBag<JobRun> pendingDependents = [];

    private JobRun(
        JobDefinition jobDefinition,
        object? parameter,
        Action<JobRun> progressReporter)
    : this(null, jobDefinition, parameter, progressReporter)
    { }

    private JobRun(
        JobRun? parentJob,
        JobDefinition jobDefinition,
        object? parameter,
        Action<JobRun> progressReporter)
    {
        var jobRunId = Guid.NewGuid();

        JobRunId = jobRunId;
        ParentJobRunId = parentJob is not null ? parentJob.JobRunId : null;
        IsOrchestrationRoot = parentJob is null;
        CorrelationId = parentJob is not null ? parentJob.CorrelationId : Guid.NewGuid();
        JobDefinition = jobDefinition;
        Parameter = parameter ?? jobDefinition.Parameter;

        this.progressReporter = progressReporter;
        rootJob = parentJob is not null ? parentJob.rootJob : this;

        Initialize();
    }

    internal JobPriority Priority { get; set; } = JobPriority.Normal;

    public Guid JobRunId { get; }
    public Guid? ParentJobRunId { get; }
    public JobDefinition JobDefinition { get; }
    public Guid CorrelationId { get; }
    public bool IsOrchestrationRoot { get; }
    public CancellationToken CancellationToken { get; set; }
    public DateTimeOffset? RunAt { get; set; }

    /// <summary>
    /// At the moment of processing, if the difference between the current time and the scheduled time exceeds the
    /// expiration period (grace period), the job is considered expired and should not be processed. Because the job is not processed,
    /// but it has been dequeued then essentially the job is dropped.
    /// </summary>
    public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(10);
    public bool IsExpired(TimeProvider timeProvider) => RunAt.HasValue && timeProvider.GetUtcNow() - RunAt.Value > Expiry;
    public bool IsOneTimeJob { get; set; }
    public object? Parameter { get; }
    public object? ParentOutput { get; set; }
    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);
    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);

    public static JobRun Create(
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition)
    => new(jobDefinition, jobDefinition.Parameter, progressReporter);

    public static JobRun Create(
        Action<JobRun> progressReporter,
        JobDefinition jobDefinition,
        object? parameter,
        CancellationToken token)
    => new(jobDefinition, parameter, progressReporter)
    {
        CancellationToken = token,
    };

    public JobRun CreateDependent(
        JobDefinition jobDefinition,
        object? parameter,
        CancellationToken token)
    {
        JobRun run = new(this, jobDefinition, parameter, progressReporter)
        {
            CancellationToken = token,
        };

        pendingDependents.Add(run);

        return run;
    }

    private void Initialize()
    {
        OnStateChanged += (jr, state) =>
        {
            switch (state.Type)
            {
                case JobStateType.Completed:
                    jr.JobDefinition.OnCompletion?.Invoke(jr.JobDefinition);
                    break;
                case JobStateType.Faulted:
                    jr.JobDefinition.OnFailure?.Invoke(jr.JobDefinition, state.Message);
                    break;
                case JobStateType.Running:
                    jr.JobDefinition.OnRunning?.Invoke(jr.JobDefinition);
                    break;
                case JobStateType.Retrying:
                case JobStateType.Scheduled:
                case JobStateType.Cancelled:
                case JobStateType.Expired:
                case JobStateType.Crashed:
                case JobStateType.Completing:
                case JobStateType.Initializing:
                case JobStateType.WaitingForDependency:
                case JobStateType.NotStarted:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state.Type, "Unexpected JobStateType value");
            }

            progressReporter(jr);
        };

        AddState(new JobState(JobStateType.NotStarted));
    }

    public bool RootJobHasPendingDependentJobs => rootJob.HasPendingDependentJobs();

    // State change logic
    public bool IsCompleted => States.Exists(s => IsFinalState(s.Type));
    public bool CanRun => CanInitiateRun(CurrentState);
    public bool IsCancellable => CanBeCancelled(CurrentState);
    public JobState CurrentState => States.LastOrDefault();
    public List<JobState> States { get; } = [];
    public event Action<JobRun, JobState>? OnStateChanged;

    public void AddState(JobState state)
    {
        States.Add(state);
        OnStateChanged?.Invoke(this, state);
    }

    public void NotifyStateChange(JobStateType type, string message = "")
    {
        if (CurrentState.Type == type || IsCompleted)
            return;

        var state = new JobState(type, message);
        AddState(state);
    }

    private static bool IsFinalState(JobStateType stateType) =>
        stateType is
        JobStateType.Completed or
        JobStateType.Cancelled or
        JobStateType.Faulted or
        JobStateType.Crashed or
        JobStateType.Expired;

    private static bool CanInitiateRun(JobStateType stateType) =>
        stateType is
        JobStateType.Initializing or
        JobStateType.Retrying;

    private static bool CanBeCancelled(JobStateType stateType) =>
        stateType is
        JobStateType.NotStarted or
        JobStateType.Scheduled ||
        CanInitiateRun(stateType);

    private bool HasPendingDependentJobs()
    {
        return !pendingDependents.IsEmpty && pendingDependents.Any(j => !j.IsCompleted || j.HasPendingDependentJobs());
    }
}
