using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace NCronJob;

internal abstract partial class RetryPolicyCreatorBase : IPolicyCreator, IInitializablePolicyCreator
{
    private ILogger logger = NullLogger.Instance;
    private TimeProvider timeProvider = TimeProvider.System;

    /// <inheritdoc />
    public void Initialize(IServiceProvider serviceProvider)
    {
        logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
        timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2) =>
        RetryPolicyFactory.Create(
            timeProvider,
            maxRetryAttempts,
            retryAttempt => GetDelay(retryAttempt, delayFactor),
            (exception, timeSpan, retryCount) => LogRetryAttempt(exception?.Message, timeSpan, retryCount));

    protected abstract TimeSpan GetDelay(int retryAttempt, double delayFactor);

    [LoggerMessage(LogLevel.Warning, "Retry {RetryCount} due to error: {Message}. Retrying after {TimeSpan}.")]
    private partial void LogRetryAttempt(string? message, TimeSpan timeSpan, int retryCount);
}
