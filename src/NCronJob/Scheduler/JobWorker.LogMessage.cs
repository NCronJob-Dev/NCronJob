using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobWorker
{
    [LoggerMessage(LogLevel.Trace, $"{nameof(JobQueueManager)} was disposed while awaiting next task execution.")]
    private partial void LogJobQueueManagerDisposed();

    [LoggerMessage(LogLevel.Trace, "Worker for queue '{QueueName}' stopped due to cancellation.")]
    private partial void LogWorkerCancelled(string queueName);
}
