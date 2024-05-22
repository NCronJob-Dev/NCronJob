using NCronJob;

namespace RunOnceSample;

public partial class RunAtStartJob2 : IJob
{
    private readonly ILogger<RunAtStartJob2> logger;

    public RunAtStartJob2(ILogger<RunAtStartJob2> logger) => this.logger = logger;

    public Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage("Hello from the startup job");

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "The input parameter is : {Msg}")]
    private partial void LogMessage(string msg);
}

