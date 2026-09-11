using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace NCronJob;

/// <summary>
/// A policy creator that configures a fixed interval retry policy.
/// </summary>
internal partial class FixedIntervalRetryPolicyCreator : IPolicyCreator, IInitializablePolicyCreator
{
    private ILogger<FixedIntervalRetryPolicyCreator> logger = NullLogger<FixedIntervalRetryPolicyCreator>.Instance;
    private TimeProvider timeProvider = TimeProvider.System;

    /// <inheritdoc />
    public void Initialize(IServiceProvider serviceProvider)
    {
        logger = serviceProvider.GetRequiredService<ILogger<FixedIntervalRetryPolicyCreator>>();
        timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2) =>
        // Here, delayFactor will represent the fixed number of seconds between retries
        RetryPolicyFactory.Create(
            timeProvider,
            maxRetryAttempts,
            _ => TimeSpan.FromSeconds(delayFactor),
            (exception, timeSpan, retryCount) => LogRetryAttempt(exception?.Message, timeSpan, retryCount));

    [LoggerMessage(LogLevel.Warning, "Retry {RetryCount} due to error: {Message}. Retrying after {TimeSpan}.")]
    private partial void LogRetryAttempt(string? message, TimeSpan timeSpan, int retryCount);
}
