using Polly;

namespace NCronJob;

/// <summary>
/// Decorates a class with a retry policy using an exponential back-off strategy provided by the <see cref="ExponentialBackoffPolicyCreator"/>.
/// This attribute is typically applied to classes that implement jobs or operations which are susceptible to transient failures.
/// </summary>
/// <remarks>
/// The exponential back-off strategy increases the delay between retry attempts exponentially, which is useful for scenarios where repeated failures
/// are likely to be resolved by allowing more time before retrying.
/// </remarks>
public sealed class RetryPolicyAttribute : RetryPolicyBaseAttribute
{
    /// <summary>
    /// Gets the type of policy creator used to generate the retry policy.
    /// The type of policy determines how retries are performed, such as using
    /// <see cref="PolicyType.ExponentialBackoff"/> for exponential delay increases between retries,
    /// or <see cref="PolicyType.FixedInterval"/> for consistent delay periods between retries.
    /// </summary>
    public PolicyType PolicyCreatorType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicyAttribute"/> class with specified retry count and policy type.
    /// </summary>
    /// <param name="retryCount">The maximum number of retry attempts.</param>
    /// <param name="policyCreatorType">The type of retry policy to create, as defined by the <see cref="PolicyType"/> enum.</param>
    public RetryPolicyAttribute(int retryCount = 3, PolicyType policyCreatorType = PolicyType.ExponentialBackoff)
    {
        RetryCount = retryCount;
        PolicyCreatorType = policyCreatorType;
    }

    internal override IAsyncPolicy CreatePolicy(IServiceProvider serviceProvider)
    {
        var factory = new PolicyCreatorFactory(serviceProvider);
        return factory.CreatePolicy(PolicyCreatorType, RetryCount, 2);
    }
}
