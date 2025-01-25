using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutor
{
    [LoggerMessage(LogLevel.Debug, "Running job: '{JobType}' with correlation id: {CorrelationId}.")]
    private partial void LogRunningJob(Type jobType, Guid correlationId);

    [LoggerMessage(LogLevel.Debug, "Skip as executor is disposed.")]
    private partial void LogSkipAsDisposed();

    [LoggerMessage(LogLevel.Warning, "The Job type '{JobType}' was not registered so an instance was created. Please register '{JobType}' for improved performance.")]
    private partial void LogUnregisteredJob(Type jobType);

    [LoggerMessage(LogLevel.Warning, "The exception handler '{JobType}' throw an exception.")]
    private partial void LogExceptionHandlerError(Type jobType);

    [LoggerMessage(LogLevel.Error, "The job '{JobType}' with correlation id: {CorrelationId} failed to execute.")]
    private partial void LogJobFailed(Type jobType, Guid correlationId);
}
