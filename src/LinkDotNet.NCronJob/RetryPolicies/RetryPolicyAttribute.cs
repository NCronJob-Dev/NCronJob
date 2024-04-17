namespace LinkDotNet.NCronJob;

/// <summary>
/// Decorates a class with a retry policy using an exponential back-off strategy provided by the <see cref="ExponentialBackoffPolicyCreator"/>.
/// This attribute is typically applied to classes that implement jobs or operations which are susceptible to transient failures.
/// </summary>
/// <remarks>
/// The exponential back-off strategy increases the delay between retry attempts exponentially, which is useful for scenarios where repeated failures
/// are likely to be resolved by allowing more time before retrying.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RetryPolicyAttribute : RetryPolicyAttribute<ExponentialBackoffPolicyCreator>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicyAttribute"/> class with a specified number of retries.
    /// </summary>
    /// <param name="retryCount">The number of retries. The default value is 3.</param>
    public RetryPolicyAttribute(int retryCount = 3)
        : base(retryCount, 2) => RetryCount = retryCount;
}
