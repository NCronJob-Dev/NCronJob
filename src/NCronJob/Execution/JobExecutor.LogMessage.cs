using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutor
{
    [LoggerMessage(LogLevel.Debug, "Running job: '{JobType}'.")]
    private partial void LogRunningJob(Type jobType);

    [LoggerMessage(LogLevel.Debug, "Skip as executor is disposed.")]
    private partial void LogSkipAsDisposed();
}
