using NCrontab;

namespace LinkDotNet.NCronJob;

internal sealed record RegistryEntry(
    Type Type,
    JobExecutionContext Context,
    CrontabSchedule? CrontabSchedule,
    JobPriority Priority = JobPriority.Normal);
