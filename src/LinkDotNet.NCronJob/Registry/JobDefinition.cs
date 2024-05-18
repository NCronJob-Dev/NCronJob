using Cronos;

namespace LinkDotNet.NCronJob;

internal sealed record JobDefinition(
    Type Type,
    object? Parameter,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone,
    int JobExecutionCount = 0,
    JobPriority Priority = JobPriority.Normal)
{
    private int jobExecutionCount = JobExecutionCount;

    public CancellationToken CancellationToken { get; set; }

    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);

    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);
}
