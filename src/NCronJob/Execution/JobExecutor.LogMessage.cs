using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutor
{
    [LoggerMessage(LogLevel.Debug, "Running job: '{JobName}' with correlation id: {CorrelationId}.")]
    private partial void LogRunningJob(string jobName, Guid correlationId);

    [LoggerMessage(LogLevel.Debug, "Skip as executor is disposed.")]
    private partial void LogSkipAsDisposed();

    [LoggerMessage(LogLevel.Error, "The job '{JobName}' with correlation id {CorrelationId} failed to execute.")]
    private partial void LogJobFailed(string jobName, Guid correlationId);

    [LoggerMessage(LogLevel.Error, "The notification handler of job '{JobName}' threw an exception.")]
    private partial void LogNotificationHandlerFailed(string jobName, Exception exception);

    [LoggerMessage(LogLevel.Error, "Disposing the services of job '{JobName}' threw an exception.")]
    private partial void LogScopeDisposalFailed(string jobName, Exception exception);
}
