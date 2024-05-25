using NCronJob;

namespace RunOnceSample;

public partial class RunAtStartJobHandler : IJobNotificationHandler<RunAtStartJob>
{
    private readonly ILogger<RunAtStartJobHandler> logger;

    public RunAtStartJobHandler(ILogger<RunAtStartJobHandler> logger) => this.logger = logger;

    public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage();
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "RunAtStartJob is done")]
    private partial void LogMessage();
}
