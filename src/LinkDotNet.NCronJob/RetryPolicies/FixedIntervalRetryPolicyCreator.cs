using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace LinkDotNet.NCronJob;

/// <summary>
/// A policy creator that configures a fixed interval retry policy.
/// </summary>
public partial class FixedIntervalRetryPolicyCreator : IPolicyCreator, IInitializablePolicyCreator
{
#pragma warning disable CS8618
    private ILogger<FixedIntervalRetryPolicyCreator> logger;

    /// <inheritdoc />
    public void Initialize(IServiceProvider serviceProvider) =>
        logger = serviceProvider.GetRequiredService<ILogger<FixedIntervalRetryPolicyCreator>>();

    /// <inheritdoc />
    public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2) =>
        // Here, delayFactor will represent the fixed number of seconds between retries
        Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxRetryAttempts,
                _ => TimeSpan.FromSeconds(delayFactor),  // Fixed delay between retries
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    LogRetryAttempt(exception.Message, timeSpan, retryCount);
                });

    [LoggerMessage(LogLevel.Warning, "Retry {RetryCount} due to error: {Message}. Retrying after {TimeSpan}.")]
    private partial void LogRetryAttempt(string message, TimeSpan timeSpan, int retryCount);
}
