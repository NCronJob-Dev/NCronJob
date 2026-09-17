using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace NCronJob;

/// <summary>
/// A policy creator that configures an exponential back-off retry policy.
/// </summary>
internal sealed partial class ExponentialBackoffPolicyCreator : IPolicyCreator, IInitializablePolicyCreator
{
    private ILogger<ExponentialBackoffPolicyCreator> logger = NullLogger<ExponentialBackoffPolicyCreator>.Instance;
    private TimeProvider timeProvider = TimeProvider.System;

    /// <inheritdoc />
    public void Initialize(IServiceProvider serviceProvider)
    {
        logger = serviceProvider.GetRequiredService<ILogger<ExponentialBackoffPolicyCreator>>();
        timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2) =>
        RetryPolicyFactory.Create(
            timeProvider,
            maxRetryAttempts,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(delayFactor, retryAttempt)),
            (exception, timeSpan, retryCount) => LogRetryAttempt(exception?.Message, timeSpan, retryCount));

    [LoggerMessage(LogLevel.Warning, "Retry {RetryCount} due to error: {Message}. Retrying after {TimeSpan}.")]
    private partial void LogRetryAttempt(string? message, TimeSpan timeSpan, int retryCount);
}
