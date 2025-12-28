using NCronJob;

namespace DashboardSample.Jobs;

public class DataProcessingJob : IJob
{
    private readonly ILogger<DataProcessingJob> logger;

    public DataProcessingJob(ILogger<DataProcessingJob> logger)
    {
        this.logger = logger;
    }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        var parameter = context.Parameter as string ?? "No parameter provided";
        logger.LogInformation("Processing data with parameter: {Parameter}", parameter);
        await Task.Delay(3000, token); // Simulate data processing
        logger.LogInformation("Data processing completed");
    }
}
