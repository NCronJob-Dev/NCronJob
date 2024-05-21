using NCronJob;

namespace NCronJobSample;

public partial class HelloWorldJobHandler : IJobNotificationHandler<PrintHelloWorldJob>
{
    private readonly ILogger<HelloWorldJobHandler> logger;

    public HelloWorldJobHandler(ILogger<HelloWorldJobHandler> logger) => this.logger = logger;

    public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage(context.Output);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "PrintHelloWorldJob is done and outputs: {Output}")]
    private partial void LogMessage(object? output);
}
