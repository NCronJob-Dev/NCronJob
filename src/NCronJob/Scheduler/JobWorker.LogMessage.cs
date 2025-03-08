using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobWorker
{
    [LoggerMessage(LogLevel.Trace, "Next run of job '{JobName}' is at {NextRun:o}")]
    private partial void LogNextJobRun(string jobName, DateTimeOffset nextRun);

    [LoggerMessage(LogLevel.Debug, "Running job '{JobName}'.")]
    private partial void LogRunningJob(string jobName);

    [LoggerMessage(LogLevel.Debug, "Job '{JobName}' completed successfully")]
    private partial void LogCompletedJob(string jobName);

    [LoggerMessage(LogLevel.Warning, "Exception occurred in job '{JobName}': {Message}")]
    private partial void LogExceptionInJob(string message, string jobName);

    [LoggerMessage(LogLevel.Trace, "Cancellation requested for CronScheduler from stopToken.")]
    private partial void LogCancellationRequestedInJob();

    [LoggerMessage(LogLevel.Trace, "Operation was cancelled.")]
    private partial void LogCancellationOperationInJob();

    [LoggerMessage(LogLevel.Trace, "Dequeuing job {JobName} because it has exceeded the expiration period.")]
    private partial void LogDequeuingExpiredJob(string jobName);

    [LoggerMessage(LogLevel.Trace, $"{nameof(JobQueueManager)} was disposed while awaiting next task execution.")]
    private partial void LogJobQueueManagerDisposed();
}
