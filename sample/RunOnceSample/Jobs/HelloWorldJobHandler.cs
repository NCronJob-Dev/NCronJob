using NCronJob;

namespace RunOnceSample;

public partial class RunAtStartJobHandler : IJobNotificationHandler<RunAtStartJob>
{
    private readonly ILogger<RunAtStartJobHandler> logger;

    public RunAtStartJobHandler(ILogger<RunAtStartJobHandler> logger) => this.logger = logger;

    public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage(context.Output);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "PrintHelloWorldJob is done and outputs: {Output}")]
    private partial void LogMessage(object? output);
}
