using Polly;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal interface IRetryHandler
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, JobExecutionContext runContext, CancellationToken cancellationToken);
}

/// <summary>
/// Manages retries for operations prone to transient failures using Polly's retry policies.
/// </summary>
/// <remarks>
/// This handler is configured to use retry policies dynamically based on attributes applied to job types,
/// allowing for flexible retry strategies such as fixed intervals or exponential back-off. The handler
/// is typically registered as a singleton to efficiently use resources and ensure consistent application
/// of retry policies. Retries and their outcomes are logged to facilitate debugging and monitoring.
/// </remarks>
/// <example>
/// Usage:
/// <code>
/// [RetryPolicy(retryCount: 3, delayFactor: 2)]
/// public class MyJob
/// {
///     public async Task RunAsync(JobExecutionContext context, CancellationToken token)
///     {
///         // Job logic that may require retries
///     }
/// }
///
/// </code>
/// </example>
internal sealed partial class RetryHandler : IRetryHandler
{
    private readonly ILogger<RetryHandler> logger;
    private readonly IServiceProvider serviceProvider;

    public RetryHandler(
        ILogger<RetryHandler> logger,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, JobExecutionContext runContext, CancellationToken cancellationToken)
    {
        try
        {
            var jobDefinition = runContext.JobRun.JobDefinition;
            var retryPolicy = jobDefinition.RetryPolicy?.CreatePolicy(serviceProvider) ?? Policy.NoOpAsync();

            // Execute the operation using the given retry policy
            await retryPolicy.ExecuteAsync(ct =>
            {
                runContext.Attempts++;
                if (runContext.Attempts > 1)
                {
                    runContext.JobRun.NotifyStateChange(JobStateType.Retrying);
                    LogRetryAttempt(runContext.Attempts, jobDefinition.JobName);
                }
                return operation(ct);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var jobName = runContext.JobRun.JobDefinition.JobName;
            LogCancellationOperationInJob(jobName);
        }
        catch (Exception ex)
        {
            LogRetryHandlerException(ex.Message);
            throw; // Ensure exceptions are not swallowed if not handled internally
        }
    }

    [LoggerMessage(LogLevel.Error, "Error occurred during an operation with retries. {Message}")]
    private partial void LogRetryHandlerException(string message);

    [LoggerMessage(LogLevel.Debug, "Attempt {RetryCount} for {JobName}")]
    private partial void LogRetryAttempt(int retryCount, string jobName);

    [LoggerMessage(LogLevel.Trace, "Operation was cancelled for {JobName}.")]
    private partial void LogCancellationOperationInJob(string jobName);
}
