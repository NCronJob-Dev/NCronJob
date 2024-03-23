using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

internal sealed partial class CronScheduler
{
    [LoggerMessage(LogLevel.Debug, "Running job: '{JobType}' in isolation level '{IsolationLevel}'")]
    private partial void LogRunningJob(Type jobType, IsolationLevel isolationLevel);

    [LoggerMessage(LogLevel.Debug, "Next run of job '{JobType}' is at {NextRun} UTC")]
    private partial void LogNextJobRun(Type jobType, DateTime nextRun);

    [LoggerMessage(LogLevel.Trace, "Begin getting next job runs")]
    private partial void LogBeginGetNextJobRuns();

    [LoggerMessage(LogLevel.Debug, "Found {Count} instant job runs")]
    private partial void LogInstantJobRuns(int count);

    [LoggerMessage(LogLevel.Trace, "Checking if '{JobType}' should on next run.")]
    private partial void LogCronJobGetsScheduled(Type jobType);

    [LoggerMessage(LogLevel.Trace, "End getting next job runs")]
    private partial void LogEndGetNextJobRuns();

    [LoggerMessage(LogLevel.Error, "The type '{Type}' is not registered. Register the service via 'AddCronJob<{Type}>()'.")]
    private partial void LogJobNotRegistered(Type type);
}
