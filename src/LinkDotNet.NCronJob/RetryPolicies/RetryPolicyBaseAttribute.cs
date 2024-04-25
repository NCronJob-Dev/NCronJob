using Polly;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Abstract base class for defining retry policy attributes, encapsulating common parameters like retry count and delay factor.
/// Derived classes must implement the creation of specific retry policies tailored to operational needs.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public abstract class RetryPolicyBaseAttribute : Attribute
{
    /// <summary>
    /// The factor that determines the delay between retry attempts.
    /// This value can influence the duration between retries, for example, in an exponential backoff strategy, 
    /// where the delay increases exponentially with each attempt.
    /// </summary>
    public double DelayFactor { get; protected set; }

    /// <summary>
    /// Gets the number of retry attempts before giving up. The default is 3 retries.
    /// A retry is considered an attempt to execute the operation again after a failure.
    /// </summary>
    /// <value>
    /// The number of times to retry the decorated operation if it fails, before failing permanently.
    /// </value>
    public int RetryCount { get; protected set; }

    /// <summary>
    /// When implemented in a derived class, creates an <see cref="IAsyncPolicy"/> that defines the retry behavior for an operation.
    /// This method must be implemented to return a specific policy instance based on the retry settings and the operational context.
    /// </summary>
    /// <param name="serviceProvider">The service provider that can be used to resolve dependencies needed by the policy.</param>
    /// <returns>An instance of <see cref="IAsyncPolicy"/> configured according to the retry settings.</returns>
    internal abstract IAsyncPolicy CreatePolicy(IServiceProvider serviceProvider);
}
