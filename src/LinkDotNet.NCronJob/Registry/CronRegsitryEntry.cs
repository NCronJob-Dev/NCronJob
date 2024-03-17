using NCrontab;

namespace LinkDotNet.NCronJob;

internal abstract record RegistryEntry(Type Type, JobExecutionContext Context);
internal sealed record InstantEntry(Type Type, JobExecutionContext Context) : RegistryEntry(Type, Context);
internal sealed record CronRegistryEntry(Type Type, JobExecutionContext Context, CrontabSchedule CrontabSchedule)
    : RegistryEntry(Type, Context);
