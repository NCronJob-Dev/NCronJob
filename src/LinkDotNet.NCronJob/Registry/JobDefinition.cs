using Cronos;

namespace LinkDotNet.NCronJob;

internal sealed record JobDefinition(
    Type Type,
    object? Parameter,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone,
    int JobExecutionCount = 0,
    JobPriority Priority = JobPriority.Normal,
    string? JobName = null,
    RetryPolicyAttribute? RetryPolicy = null,
    SupportsConcurrencyAttribute? ConcurrencyPolicy = null)
{
    private int jobExecutionCount = JobExecutionCount;

    public CancellationToken CancellationToken { get; set; }

    public string JobName { get; } = JobName ?? Type.Name;

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job. This helps with concurrency management.
    /// </summary>
    public string JobFullName => JobName == Type.Name ? Type.FullName ?? JobName : $"{typeof(DynamicJobFactory).Namespace}.{JobName}";

    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);

    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);
}
