using Cronos;

namespace NCronJob;

internal record JobDefinition(
    Type Type,
    object? Parameter,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone,
    string? JobName = null,
    JobExecutionAttributes? JobPolicyMetadata = null)
{

    public bool Completed => States.Exists(s => IsFinalState(s.Type));
    public bool IsStartupJob { get; set; }

    public string JobName { get; } = JobName ?? Type.Name;

    public List<JobDefinition> RunWhenSuccess { get; set; } = [];

    public List<JobDefinition> RunWhenFaulted { get; set; } = [];

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    public string JobFullName => JobName == Type.Name
        ? Type.FullName ?? JobName
        : $"{typeof(DynamicJobFactory).Namespace}.{JobName}";

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
            case JobStateType.Expired:
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Type, "Unexpected JobStateType value");
        }
    }

    private static bool IsFinalState(JobStateType stateType) =>
        stateType is JobStateType.Completed or JobStateType.Cancelled or JobStateType.Failed or JobStateType.Crashed;
}
