using Polly;
using Polly.Retry;

namespace NCronJob;

internal static class RetryPolicyFactory
{
    public static IAsyncPolicy Create(
        TimeProvider timeProvider,
        int maxRetryAttempts,
        Func<int, TimeSpan> delayForRetry,
        Action<Exception?, TimeSpan, int> onRetry)
    {
        if (maxRetryAttempts <= 0)
        {
            return Policy.NoOpAsync();
        }

        return new ResiliencePipelineBuilder { TimeProvider = timeProvider }
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetryAttempts,
                DelayGenerator = args => new ValueTask<TimeSpan?>(delayForRetry(args.AttemptNumber + 1)),
                ShouldHandle = args => new ValueTask<bool>(
                    args.Outcome.Exception is not null && !args.Context.CancellationToken.IsCancellationRequested),
                OnRetry = args =>
                {
                    onRetry(args.Outcome.Exception, args.RetryDelay, args.AttemptNumber + 1);
                    return default;
                },
            })
            .Build()
            .AsAsyncPolicy();
    }
}
