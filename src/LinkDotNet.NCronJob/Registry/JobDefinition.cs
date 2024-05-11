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
    RetryPolicyAttribute? RetryPolicy = null)
{
    private int jobExecutionCount = JobExecutionCount;

    public CancellationToken CancellationToken { get; set; }

    public string JobName { get; } = JobName ?? Type.Name;

    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);

    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);
}
