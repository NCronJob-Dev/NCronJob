
namespace NCronJob;

internal class JobRun
{
    private int jobExecutionCount;

    private JobRun() => Initialize();

    internal JobPriority Priority { get; set; } = JobPriority.Normal;

    public required Guid JobRunId { get; init; }
    public required JobDefinition JobDefinition { get; init; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
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
    public object? Parameter { get; init; }
    public object? ParentOutput { get; set; }
    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);
    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);

    public static JobRun Create(JobDefinition jobDefinition) =>
        new()
        {
            JobRunId = Guid.NewGuid(),
            JobDefinition = jobDefinition,
            Parameter = jobDefinition.Parameter
        };

    public static JobRun Create(JobDefinition jobDefinition, object? parameter, CancellationToken token) =>
        new()
        {
            JobRunId = Guid.NewGuid(),
            JobDefinition = jobDefinition,
            Parameter = parameter,
            CancellationToken = token
        };

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
        };
        AddState(new JobState(JobStateType.NotStarted));
    }

    // State change logic
    public bool IsCompleted => States.Exists(s => IsFinalState(s.Type));
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
        if (CurrentState.Type == type)
            return;

        var state = new JobState(type, message);
        AddState(state);
    }

    private static bool IsFinalState(JobStateType stateType) =>
        stateType is JobStateType.Completed or JobStateType.Cancelled or JobStateType.Faulted or JobStateType.Crashed;
}
