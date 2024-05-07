using Cronos;

namespace LinkDotNet.NCronJob;

internal sealed record RegistryEntry(
    Type Type,
    object? Output,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone,
    int JobExecutionCount = 0,
    JobPriority Priority = JobPriority.Normal)
{
    private int jobExecutionCount = JobExecutionCount;

    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);

    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);
}
