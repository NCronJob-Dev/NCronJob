using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace NCronJob;

/// <summary>
/// A policy creator that configures an exponential back-off retry policy.
/// </summary>
internal partial class ExponentialBackoffPolicyCreator : IPolicyCreator, IInitializablePolicyCreator
{
    private ILogger<ExponentialBackoffPolicyCreator> logger = default!;

    /// <inheritdoc />
    public void Initialize(IServiceProvider serviceProvider) =>
        logger = serviceProvider.GetRequiredService<ILogger<ExponentialBackoffPolicyCreator>>();

    /// <inheritdoc />
    public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2) =>
        Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(delayFactor, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    LogRetryAttempt(exception.Message, timeSpan, retryCount);
                });

    [LoggerMessage(LogLevel.Warning, "Retry {RetryCount} due to error: {Message}. Retrying after {TimeSpan}.")]
    private partial void LogRetryAttempt(string message, TimeSpan timeSpan, int retryCount);
}
