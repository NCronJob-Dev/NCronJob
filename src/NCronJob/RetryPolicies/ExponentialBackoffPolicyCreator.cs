namespace NCronJob;

/// <summary>
/// A policy creator that configures an exponential back-off retry policy.
/// </summary>
internal sealed class ExponentialBackoffPolicyCreator : RetryPolicyCreatorBase
{
    protected override TimeSpan GetDelay(int retryAttempt, double delayFactor) =>
        TimeSpan.FromSeconds(Math.Pow(delayFactor, retryAttempt));
}
