using Cronos;

namespace NCronJob;

internal record JobDefinition(
    Type Type,
    object? Parameter,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone,
    JobPriority Priority = JobPriority.Normal,
    string? JobName = null,
    JobExecutionAttributes? JobPolicyMetadata = null)
{
    private int jobExecutionCount;
    public CancellationToken CancellationToken { get; set; }

    public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(10);
    public DateTimeOffset? RunAt { get; set; }
    public bool Completed => States.Exists(s => IsFinalState(s.Type));
    public bool IsExpired => RunAt.HasValue && DateTimeOffset.UtcNow - RunAt.Value > Expiry;
    public bool IsStartupJob { get; set; }
    public bool IsOneTimeJob { get; set; }

    public string JobName { get; } = JobName ?? Type.Name;

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    public string JobFullName => JobName == Type.Name
        ? Type.FullName ?? JobName
        : $"{typeof(DynamicJobFactory).Namespace}.{JobName}";

    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);

    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);

    private JobExecutionAttributes JobPolicyMetadata { get; } = JobPolicyMetadata ?? new JobExecutionAttributes(Type);
    public RetryPolicyAttribute? RetryPolicy => JobPolicyMetadata?.RetryPolicy;
    public SupportsConcurrencyAttribute? ConcurrencyPolicy => JobPolicyMetadata?.ConcurrencyPolicy;

    // Hooks for specific state changes
    public Action<JobDefinition>? OnCompletion { get; set; }
    public Action<JobDefinition>? OnFailure { get; set; }
    public Action<JobDefinition>? OnRunning { get; set; }
    public List<JobState> States { get; } = [];
    public event Action<JobDefinition, JobState>? OnStateChanged;

    public void AddState(JobState state)
    {
        States.Add(state);
        OnStateChanged?.Invoke(this, state);
    }
    public void NotifyStateChange(JobState state)
    {
        AddState(state);
        switch (state.Type)
        {
            case JobStateType.Completed:
                OnCompletion?.Invoke(this);
                break;
            case JobStateType.Failed:
                OnFailure?.Invoke(this);
                break;
            case JobStateType.Running:
                OnRunning?.Invoke(this);
                break;
            case JobStateType.Scheduled:
                break;
            case JobStateType.Cancelled:
                break;
            case JobStateType.Crashed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Type, "Unexpected JobStateType value");
        }
    }

    private static bool IsFinalState(JobStateType stateType) =>
        stateType is JobStateType.Completed or JobStateType.Cancelled or JobStateType.Failed or JobStateType.Crashed;
}


internal enum JobStateType
{
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled,
    Crashed
}

internal class JobState
{
    public JobStateType Type { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Message { get; private set; }

    public JobState(JobStateType type, string message = "")
    {
        Type = type;
        Timestamp = DateTime.UtcNow;
        Message = message;
    }
}
