using Polly;
#pragma warning disable CA1813
namespace LinkDotNet.NCronJob;

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
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class RetryPolicyAttribute<TPolicyCreator> : Attribute where TPolicyCreator : IPolicyCreator, new()
{
    /// <summary>
    /// The factor that determines the delay between retries
    /// </summary>
    public double DelayFactor { get; internal set; }

    /// <summary>
    /// Gets the number of retry attempts before giving up. The default is 3 retries.
    /// A retry is considered an attempt to execute the operation again after a failure.
    /// </summary>
    /// <value>
    /// The number of times to retry the decorated operation if it fails, before failing permanently.
    /// </value>
    public int RetryCount { get; internal set; }

    internal IAsyncPolicy CreatePolicy(IServiceProvider serviceProvider)
    {
        var factory = new PolicyCreatorFactory(serviceProvider);
        var creator = factory.Create<TPolicyCreator>();
        return creator.CreatePolicy(RetryCount, DelayFactor);
    }

    /// <inheritdoc />
    public RetryPolicyAttribute(int retryCount = 3, double delayFactor = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retryCount);
        this.DelayFactor = delayFactor;
        this.RetryCount = retryCount;
    }
}
