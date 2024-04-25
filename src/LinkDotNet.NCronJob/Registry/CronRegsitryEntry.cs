using Cronos;

namespace LinkDotNet.NCronJob;

internal sealed record RegistryEntry(
    Type Type,
    object? Output,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone,
    int JobExecutionCount = 0,
    JobPriority Priority = JobPriority.Normal);
