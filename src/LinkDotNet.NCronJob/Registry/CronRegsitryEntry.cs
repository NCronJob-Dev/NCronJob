using NCrontab;

namespace LinkDotNet.NCronJob;

internal sealed record RegistryEntry(
    Type Type,
    object? Output,
    CrontabSchedule? CrontabSchedule,
    int JobExecutionCount = 0,
    JobPriority Priority = JobPriority.Normal)
{
    public int JobExecutionCount { get; set; } = JobExecutionCount;
}
