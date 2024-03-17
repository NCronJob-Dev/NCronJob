using LinkDotNet.NCronJob;

namespace NCronJobSample;

public partial class PrintHelloWorld : IJob
{
    private readonly ILogger<PrintHelloWorld> logger;

    public PrintHelloWorld(ILogger<PrintHelloWorld> logger)
    {
        this.logger = logger;
    }

    public Task Run(JobExecutionContext context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage(context.Parameter);

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Message: {Parameter}")]
    private partial void LogMessage(object? parameter);
}
