using Polly;

namespace NCronJob;

/// <summary>
/// Applies a retry policy to Jobs, specifying the type of retry policy creator to configure the retry behavior dynamically.
/// </summary>
/// <typeparam name="TPolicyCreator">
/// The type of the policy creator which must implement <see cref="IPolicyCreator"/> and have a parameterless constructor.
/// This allows the attribute to instantiate the policy creator and apply the specified retry policy.
/// </typeparam>
/// <remarks>
/// This attribute enables developers to define how retry policies are applied to specific operations or entire classes,
/// making it flexible to integrate different strategies for handling retries, such as exponential back-off or fixed interval retries.
/// </remarks>
/// <example>
/// Applying a custom retry policy to a method:
/// <code>
/// [RetryPolicyAttribute&lt;CustomRetryPolicyCreator&gt;(retryCount: 5, delayFactor: 1.5)]
/// public class MyJob
/// {
///     public async Task RunAsync(JobExecutionContext context, CancellationToken token)
///     {
///         // Unreliable Job logic that may require retries
///     }
/// }
/// </code>
/// Here, <c>CustomRetryPolicyCreator</c> defines how the retries are performed, and it must implement the <see cref="IPolicyCreator"/>.
/// </example>
public sealed class RetryPolicyAttribute<TPolicyCreator> : RetryPolicyBaseAttribute where TPolicyCreator : IPolicyCreator, new()
{
    internal override IAsyncPolicy CreatePolicy(IServiceProvider serviceProvider)
    {
        var factory = new PolicyCreatorFactory(serviceProvider);
        var policy = factory.Create<TPolicyCreator>(RetryCount, DelayFactor);
        return policy;
    }

    /// <inheritdoc />
    public RetryPolicyAttribute(int retryCount = 3, double delayFactor = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(delayFactor);
        this.DelayFactor = delayFactor;
        this.RetryCount = retryCount;
    }
}
