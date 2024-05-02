using NCrontab;

namespace LinkDotNet.NCronJob;

internal sealed record RegistryEntry(
    Type Type,
    object? Output,
    CrontabSchedule? CrontabSchedule,
    JobPriority Priority = JobPriority.Normal)
{
    private int jobExecutionCount;

    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);

    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);
}
