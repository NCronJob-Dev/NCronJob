using Cronos;

namespace NCronJob;

internal sealed record JobDefinition(
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

    public bool IsStartupJob { get; set; }

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
}
