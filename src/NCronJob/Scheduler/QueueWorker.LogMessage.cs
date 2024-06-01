using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class QueueWorker
{
    [LoggerMessage(LogLevel.Debug, "Next run of job '{JobType}' is at {NextRun}")]
    private partial void LogNextJobRun(Type jobType, DateTimeOffset nextRun);

    [LoggerMessage(LogLevel.Debug, "Running job '{JobType}'.")]
    private partial void LogRunningJob(Type jobType);

    [LoggerMessage(LogLevel.Debug, "Job completed successfully: '{JobType}'.")]
    private partial void LogCompletedJob(Type jobType);

    [LoggerMessage(LogLevel.Warning, "Exception occurred in job {JobType}: {Message}")]
    private partial void LogExceptionInJob(string message, Type jobType);

    [LoggerMessage(LogLevel.Trace, "Cancellation requested for CronScheduler from stopToken.")]
    private partial void LogCancellationRequestedInJob();

    [LoggerMessage(LogLevel.Trace, "Operation was cancelled.")]
    private partial void LogCancellationOperationInJob();

    [LoggerMessage(LogLevel.Trace, "Dequeuing job {JobName} because it has exceeded the expiration period.")]
    private partial void LogDequeuingExpiredJob(string jobName);

}
