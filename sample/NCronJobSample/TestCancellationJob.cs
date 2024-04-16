using LinkDotNet.NCronJob;
using System.Security.Cryptography;

namespace NCronJobSample;

public partial class TestCancellationJob : IJob
{
    private readonly ILogger<TestCancellationJob> logger;

    public TestCancellationJob(ILogger<TestCancellationJob> logger) => this.logger = logger;

    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        token.Register(() => LogCancellationRequestedInJob(context.Parameter));

        LogMessage(context.Parameter);

        try
        {
            // Simulate a long-running job
            for (var i = 0; i < 15; i++) // Simulate 15 units of work
            {
                if (token.IsCancellationRequested)
                {
                    LogCancellationNotice();
                    token.ThrowIfCancellationRequested(); // Properly handle the cancellation
                }

                // Simulate work by delaying a random amount of time
                var variableMs = TimeSpan.FromMilliseconds(1000 + RandomNumberGenerator.GetInt32(2000));
                await Task.Delay(variableMs, token);
                //await Task.Delay(2000, token);

                // Log each unit of work completion
                LogWorkUnitCompleted(i + 1, context.Parameter);
            }

            context.Output = "Hey there!";
        }
        catch (OperationCanceledException)
        {
            LogCancellationConfirmed(context.Parameter);
        }
    }

    [LoggerMessage(LogLevel.Information, "Message: {Parameter}")]
    private partial void LogMessage(object? parameter);

    [LoggerMessage(LogLevel.Warning, "Job cancelled by request.")]
    private partial void LogCancellationNotice();

    [LoggerMessage(LogLevel.Information, "Cancellation confirmed. Clean-up complete for {Parameter}.")]
    private partial void LogCancellationConfirmed(object? parameter);

    [LoggerMessage(LogLevel.Information, "Completed work unit {Number}/15 for {Parameter}")]
    private partial void LogWorkUnitCompleted(int number, object? parameter);

    [LoggerMessage(LogLevel.Debug, "Cancellation requested for TestCancellationJob {Parameter}.")]
    private partial void LogCancellationRequestedInJob(object? parameter);

}

