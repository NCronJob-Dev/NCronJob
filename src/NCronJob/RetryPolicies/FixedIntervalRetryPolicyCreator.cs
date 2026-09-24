namespace NCronJob;

/// <summary>
/// A policy creator that configures a fixed interval retry policy.
/// </summary>
internal sealed class FixedIntervalRetryPolicyCreator : RetryPolicyCreatorBase
{
    // Here, delayFactor represents the fixed number of seconds between retries
    protected override TimeSpan GetDelay(int retryAttempt, double delayFactor) =>
        TimeSpan.FromSeconds(delayFactor);
}
