using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class QueueWorker
{
    [LoggerMessage(LogLevel.Trace, "QueueWorker is shutting down.")]
    private partial void LogQueueWorkerShuttingDown();

    [LoggerMessage(LogLevel.Error, "An error occurred during QueueWorker.ExecuteAsync.")]
    private partial void LogQueueWorkerError(Exception ex);

    [LoggerMessage(LogLevel.Error, "An error occurred while creating a Queue Worker for job '{JobName}'.")]
    private partial void LogQueueWorkerCreationError(string jobName, Exception ex);

    [LoggerMessage(LogLevel.Information, "New queue added for job '{JobName}'.")]
    private partial void LogNewQueueAdded(string jobName);

    [LoggerMessage(LogLevel.Trace, "Job Queue Worker Cancelled '{JobName}'")]
    private partial void LogJobQueueCancelled(string jobName);

    [LoggerMessage(LogLevel.Trace, "Job Queue Worker Faulted '{JobName}'")]
    private partial void LogJobQueueFaulted(string jobName);

    [LoggerMessage(LogLevel.Trace, "Job Queue Worker Completed '{JobName}'")]
    private partial void LogJobQueueCompleted(string jobName);

    [LoggerMessage(LogLevel.Information, "QueueWorker Service is stopping.")]
    private partial void LogQueueWorkerStopping();

    [LoggerMessage(LogLevel.Information, "QueueWorker is draining. Waiting for tasks to complete.")]
    private partial void LogQueueWorkerDraining();

    [LoggerMessage(LogLevel.Trace, "Cancellation requested in job")]
    private partial void LogCancellationRequestedInJob();

    [LoggerMessage(LogLevel.Information, "Job added to queue: '{JobName}' at {RunAt:o}")]
    private partial void LogJobAddedToQueue(string jobName, DateTimeOffset? runAt);

    [LoggerMessage(LogLevel.Information, "Job removed from queue: '{JobName}' at {RunAt:o}")]
    private partial void LogJobRemovedFromQueue(string jobName, DateTimeOffset? runAt);
}
