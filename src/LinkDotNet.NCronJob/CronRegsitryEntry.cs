using NCrontab;

namespace LinkDotNet.NCronJob;

internal record RegistryEntry(Type Type);
internal sealed record InstantEntry(Type Type) : RegistryEntry(Type);
internal sealed record CronRegistryEntry(Type Type, CrontabSchedule CrontabSchedule) : RegistryEntry(Type);
