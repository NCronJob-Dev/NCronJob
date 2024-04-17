using System.Reflection;
using Polly;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

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
internal sealed partial class RetryHandler
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

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, RegistryEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var retryPolicyAttribute = entry.Type.GetCustomAttribute<RetryPolicyAttribute>();
            var retryPolicy = retryPolicyAttribute?.CreatePolicy(serviceProvider) ?? Policy.NoOpAsync();

            // Execute the operation using the given retry policy
            await retryPolicy.ExecuteAsync(() =>
            {
                entry.Context.Attempts++;
                return operation(cancellationToken);
            });
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
}
