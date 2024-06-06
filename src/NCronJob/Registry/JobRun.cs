using NCronJob;

internal class JobRun
{
    private int jobExecutionCount;

    internal JobPriority Priority { get; set; } = JobPriority.Normal;

    public required Guid JobRunId { get; init; }
    public required JobDefinition JobDefinition { get; init; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public CancellationToken CancellationToken { get; set; }
    public DateTimeOffset? RunAt { get; set; }
    public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(10);
    public bool IsExpired(TimeProvider timeProvider) => RunAt.HasValue && timeProvider.GetUtcNow() - RunAt.Value > Expiry;
    public bool IsOneTimeJob { get; set; }
    public object? Parameter { get; init; }
    public object? ParentOutput { get; set; }
    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);
    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);

    public static JobRun Create(JobDefinition jobDefinition) =>
        new JobRun
        {
            JobRunId = Guid.NewGuid(),
            JobDefinition = jobDefinition,
            Parameter = jobDefinition.Parameter
        }.Initialize();

    public static JobRun Create(JobDefinition jobDefinition, object? parameter, CancellationToken token) =>
        new JobRun
        {
            JobRunId = Guid.NewGuid(),
            JobDefinition = jobDefinition,
            Parameter = parameter,
            CancellationToken = token
        }.Initialize();

    private JobRun Initialize()
    {
        this.OnStateChanged += (jr, state) =>
        {
            switch (state.Type)
            {
                case JobStateType.Completed:
                    jr.JobDefinition.OnCompletion?.Invoke(jr.JobDefinition);
                    break;
                case JobStateType.Failed:
                    jr.JobDefinition.OnFailure?.Invoke(jr.JobDefinition);
                    break;
                case JobStateType.Running:
                    jr.JobDefinition.OnRunning?.Invoke(jr.JobDefinition);
                    break;
                case JobStateType.Scheduled:
                    break;
                case JobStateType.Cancelled:
                    break;
                case JobStateType.Expired:
                    break;
                case JobStateType.Crashed:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state.Type, "Unexpected JobStateType value");
            }
        };
        return this;
    }

    // State change logic
    public bool Completed => States.Exists(s => IsFinalState(s.Type));
    public List<JobState> States { get; } = [];
    public event Action<JobRun, JobState>? OnStateChanged;

    public void AddState(JobState state)
    {
        States.Add(state);
        OnStateChanged?.Invoke(this, state);
    }
    
    public void NotifyStateChange(JobStateType type, string message = "")
    {
        var state = new JobState(type, message);
        AddState(state);
    }

    private static bool IsFinalState(JobStateType stateType) =>
        stateType is JobStateType.Completed or JobStateType.Cancelled or JobStateType.Failed or JobStateType.Crashed;
}
