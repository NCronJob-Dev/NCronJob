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

    [LoggerMessage(LogLevel.Trace, "Job added to the queue: {JobName} at {ScheduledAt}")]
    private partial void LogJobAddedToQueue(string jobName, DateTimeOffset? scheduledAt);

    [LoggerMessage(LogLevel.Trace, "Job removed from the queue: {JobName} scheduled for {ScheduledFor}")]
    private partial void LogJobRemovedFromQueue(string jobName, DateTimeOffset? scheduledFor);

    [LoggerMessage(LogLevel.Trace, "New Job Queue added for new job type {JobTypeName}")]
    private partial void LogNewQueueAdded(string jobTypeName);

}
