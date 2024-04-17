using Cronos;

namespace LinkDotNet.NCronJob;

internal sealed record RegistryEntry(
    Type Type,
    JobExecutionContext Context,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone);
