using NCrontab;

namespace LinkDotNet.NCronJob;

internal sealed record RegistryEntry(
    Type Type,
    JobExecutionContext Context,
    IsolationLevel IsolationLevel,
    CrontabSchedule? CrontabSchedule);
