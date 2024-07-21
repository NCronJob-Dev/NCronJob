using System.Security.Cryptography;
using NCronJob;

namespace NCronJobSample;

[SupportsConcurrency(10)]  // Supports up to 10 concurrent instances
public partial class ConcurrentTaskExecutorJob : IJob
{
    private readonly ILogger<ConcurrentTaskExecutorJob> logger;

    public ConcurrentTaskExecutorJob(ILogger<ConcurrentTaskExecutorJob> logger) => this.logger = logger;

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        var jobId = context.Id.ToString();  // Unique identifier for the job instance
        LogStartingJob(jobId);

        try
        {
            // Simulate different types of work by choosing a random task type
            var taskType = RandomNumberGenerator.GetInt32(1, 4);  // Randomly pick a task type between 1 and 3
            await (taskType switch
            {
                1 => SimulateDataProcessing(jobId, token),
                2 => SimulateApiCall(jobId, token),
                3 => PerformCalculations(jobId, token),
                _ => Task.CompletedTask
            });

            LogJobCompleted(jobId);
        }
        catch (OperationCanceledException)
        {
            LogCancellationConfirmed(jobId);
        }
    }

    private async Task SimulateDataProcessing(string jobId, CancellationToken token)
    {
        // Simulate processing chunks of data
        for (var i = 0; i < 5; i++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(RandomNumberGenerator.GetInt32(200, 500), token);  // Simulate work
            LogProgress($"Data processing (Chunk {i + 1}/5)", jobId);
        }
    }

    private async Task SimulateApiCall(string jobId, CancellationToken token)
    {
        // Simulate asynchronous API calls
        for (var i = 0; i < 3; i++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(RandomNumberGenerator.GetInt32(500, 1000), token);  // Simulate API latency
            LogProgress($"API Call {i + 1}/3 completed", jobId);
        }
    }

    private async Task PerformCalculations(string jobId, CancellationToken token)
    {
        // Perform some random calculations
        for (var i = 0; i < 10; i++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(RandomNumberGenerator.GetInt32(100, 300), token);  // Simulate calculation time
            LogProgress($"Calculation {i + 1}/10", jobId);
        }
    }

    [LoggerMessage(LogLevel.Information, "Job {JobId} started.")]
    private partial void LogStartingJob(string jobId);

    [LoggerMessage(LogLevel.Information, "Job {JobId} completed.")]
    private partial void LogJobCompleted(string jobId);

    [LoggerMessage(LogLevel.Information, "Job {JobId} progress: {Message}.")]
    private partial void LogProgress(string message, string jobId);

    [LoggerMessage(LogLevel.Warning, "Cancellation confirmed for Job {JobId}.")]
    private partial void LogCancellationConfirmed(string jobId);
}
