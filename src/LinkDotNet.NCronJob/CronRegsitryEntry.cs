using NCrontab;

namespace LinkDotNet.NCronJob;

internal sealed record CronRegistryEntry(Type Type, CrontabSchedule CrontabSchedule);
