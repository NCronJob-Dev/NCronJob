using NCronJob;

namespace RunOnceSample;

public partial class RunAtStartJob : IJob
{
    private readonly ILogger<RunAtStartJob> logger;

    public RunAtStartJob(ILogger<RunAtStartJob> logger) => this.logger = logger;

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        for (var i = 0; i < 5; i++) // Simulate work
        {
            await Task.Delay(1000, token);

            LogWorkUnitCompleted(i + 1);
        }

    }

    [LoggerMessage(LogLevel.Information, "Completed work unit {Number}/5")]
    private partial void LogWorkUnitCompleted(int number);
}

