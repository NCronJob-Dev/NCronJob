using NCronJob;

namespace DashboardSample.Jobs;

public class HelloWorldJob : IJob
{
    private readonly ILogger<HelloWorldJob> logger;

    public HelloWorldJob(ILogger<HelloWorldJob> logger)
    {
        this.logger = logger;
    }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation("Hello World Job executed at {Time}", DateTime.UtcNow);
        await Task.Delay(2000, token); // Simulate some work
        logger.LogInformation("Hello World Job completed");
    }
}
