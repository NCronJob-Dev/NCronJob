using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

internal sealed partial class CronScheduler
{
    [LoggerMessage(LogLevel.Debug, "Next run of job '{JobType}' is at {NextRun} UTC")]
    private partial void LogNextJobRun(Type jobType, DateTime nextRun);

    [LoggerMessage(LogLevel.Debug, "Running job '{JobType}'.")]
    private partial void LogRunningJob(Type jobType);

    [LoggerMessage(LogLevel.Debug, "Exception occurred in job {JobType}: {Message}")]
    private partial void LogExceptionInJob(string message, Type jobType);
}
