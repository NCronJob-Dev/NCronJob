using Polly;

namespace NCronJob;

internal class PolicyCreatorFactory(IServiceProvider serviceProvider)
{
    public IAsyncPolicy CreatePolicy(PolicyType policyType, int retryCount, double delayFactor) =>
        policyType switch
        {
            PolicyType.ExponentialBackoff => Create<ExponentialBackoffPolicyCreator>(retryCount, delayFactor),
            PolicyType.FixedInterval => Create<FixedIntervalRetryPolicyCreator>(retryCount, delayFactor),
            _ => throw new ArgumentException("Unsupported policy type")
        };

    public IAsyncPolicy Create<TPolicyCreator>(int retryCount, double delayFactor) where TPolicyCreator : IPolicyCreator, new()
    {
        var creator = new TPolicyCreator();
        (creator as IInitializablePolicyCreator)?.Initialize(serviceProvider);
        return creator.CreatePolicy(retryCount, delayFactor);
    }
}
