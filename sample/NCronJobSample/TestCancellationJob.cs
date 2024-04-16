using LinkDotNet.NCronJob;
using System.Threading;

namespace NCronJobSample;

public partial class TestCancellationJob : IJob
{
    private readonly ILogger<TestCancellationJob> logger;

    public TestCancellationJob(ILogger<TestCancellationJob> logger) => this.logger = logger;

    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        await using var registration = token.Register(() =>
        {
            Console.WriteLine($"Cancellation requested for job TestCancellationJob.");
        });


        ArgumentNullException.ThrowIfNull(context);

        LogMessage(context.Parameter);

        try
        {
            // Simulate a long-running job
            for (var i = 0; i < 20; i++) // Simulate 20 units of work
            {
                if (token.IsCancellationRequested)
                {
                    LogCancellationNotice();
                    token.ThrowIfCancellationRequested(); // Properly handle the cancellation
                }

                // Simulate work by delaying
                await Task.Delay(2000, token);

                // Log each unit of work completion
                LogWorkUnitCompleted(i + 1);
            }

            context.Output = "Hey there!";
        }
        catch (OperationCanceledException)
        {
            LogCancellationConfirmed();
        }
    }

    [LoggerMessage(LogLevel.Information, "Message: {Parameter}")]
    private partial void LogMessage(object? parameter);

    [LoggerMessage(LogLevel.Warning, "Job cancelled by request.")]
    private partial void LogCancellationNotice();

    [LoggerMessage(LogLevel.Information, "Cancellation confirmed. Clean-up complete.")]
    private partial void LogCancellationConfirmed();

    [LoggerMessage(LogLevel.Information, "Completed work unit {Number}/10.")]
    private partial void LogWorkUnitCompleted(int number);
}

