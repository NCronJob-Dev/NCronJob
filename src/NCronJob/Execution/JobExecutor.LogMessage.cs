using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutor
{
    [LoggerMessage(LogLevel.Debug, "Running job: '{JobName}' with correlation id: {CorrelationId}.")]
    private partial void LogRunningJob(string jobName, Guid correlationId);

    [LoggerMessage(LogLevel.Debug, "Skip as executor is disposed.")]
    private partial void LogSkipAsDisposed();

    [LoggerMessage(LogLevel.Warning, "The job '{JobName}' was not registered so an instance was created. Please register '{JobName}' for improved performance.")]
    private partial void LogUnregisteredJob(string jobName);

    [LoggerMessage(LogLevel.Error, "The exception handler '{Type}' throw an exception.")]
    private partial void LogExceptionHandlerError(Type type);

    [LoggerMessage(LogLevel.Error, "The job '{JobName}' with correlation id {CorrelationId} failed to execute.")]
    private partial void LogJobFailed(string jobName, Guid correlationId);
}
