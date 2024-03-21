using LinkDotNet.NCronJob;

namespace NCronJobSample;

public partial class PrintHelloWorldJob : IJob
{
    private readonly ILogger<PrintHelloWorldJob> logger;

    public PrintHelloWorldJob(ILogger<PrintHelloWorldJob> logger)
    {
        this.logger = logger;
    }

    public Task Run(JobExecutionContext context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage(context.Parameter);

        context.Output = "Hey there!";

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Message: {Parameter}")]
    private partial void LogMessage(object? parameter);
}
