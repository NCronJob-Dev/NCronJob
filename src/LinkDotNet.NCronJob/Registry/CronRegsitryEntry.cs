using NCrontab;

namespace LinkDotNet.NCronJob;

internal record RegistryEntry(
    Type Type,
    JobExecutionContext Context,
    IsolationLevel IsolationLevel,
    CrontabSchedule? CrontabSchedule);
