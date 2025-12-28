using NCronJob;

namespace DashboardSample.Jobs;

public class ReportGenerationJob : IJob
{
    private readonly ILogger<ReportGenerationJob> logger;

    public ReportGenerationJob(ILogger<ReportGenerationJob> logger)
    {
        this.logger = logger;
    }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation("Generating reports...");
        await Task.Delay(5000, token);
        logger.LogInformation("Reports generated successfully");
    }
}
