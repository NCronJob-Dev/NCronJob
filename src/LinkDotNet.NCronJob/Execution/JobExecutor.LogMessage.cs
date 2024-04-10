using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

internal sealed partial class JobExecutor
{
    [LoggerMessage(LogLevel.Error,
        "The type '{Type}' is not registered. Register the service via 'AddCronJob<{Type}>()'.")]
    private partial void LogJobNotRegistered(Type type);

    [LoggerMessage(LogLevel.Debug, "Running job: '{JobType}'.")]
    private partial void LogRunningJob(Type jobType);
}
