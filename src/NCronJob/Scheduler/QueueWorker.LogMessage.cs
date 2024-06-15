using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class QueueWorker
{
    [LoggerMessage(LogLevel.Trace, "QueueWorker is shutting down.")]
    private partial void LogQueueWorkerShuttingDown();

    [LoggerMessage(LogLevel.Error, "An error occurred during QueueWorker.ExecuteAsync.")]
    private partial void LogQueueWorkerError(Exception ex);

    [LoggerMessage(LogLevel.Error, "An error occurred while creating a Queue Worker for job type {JobType}.")]
    private partial void LogQueueWorkerCreationError(string jobType, Exception ex);

    [LoggerMessage(LogLevel.Information, "New queue added for job type {JobType}.")]
    private partial void LogNewQueueAdded(string jobType);

    [LoggerMessage(LogLevel.Trace, "Job Queue Worker Cancelled {JobType}")]
    private partial void LogJobQueueCancelled(string jobType);

    [LoggerMessage(LogLevel.Trace, "Job Queue Worker Faulted {JobType}")]
    private partial void LogJobQueueFaulted(string jobType);

    [LoggerMessage(LogLevel.Trace, "Job Queue Worker Completed {JobType}")]
    private partial void LogJobQueueCompleted(string jobType);

    [LoggerMessage(LogLevel.Information, "QueueWorker Service is stopping.")]
    private partial void LogQueueWorkerStopping();

    [LoggerMessage(LogLevel.Information, "QueueWorker is draining. Waiting for tasks to complete.")]
    private partial void LogQueueWorkerDraining();

    [LoggerMessage(LogLevel.Trace, "Cancellation requested in job")]
    private partial void LogCancellationRequestedInJob();

    [LoggerMessage(LogLevel.Information, "Job added to queue: {JobType} at {RunAt}")]
    private partial void LogJobAddedToQueue(string jobType, DateTime? runAt);

    [LoggerMessage(LogLevel.Information, "Job removed from queue: {JobType} at {RunAt}")]
    private partial void LogJobRemovedFromQueue(string jobType, DateTime? runAt);
}
