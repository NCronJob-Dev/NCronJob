using Polly;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Defines a contract used to create retry policies.
/// </summary>
public interface IPolicyCreator
{
    /// <summary>
    /// Creates and configures an asynchronous retry policy.
    /// </summary>
    /// <param name="maxRetryAttempts">The maximum number of retry attempts. Defaults to 3.</param>
    /// <param name="delayFactor">The factor that determines the delay between retries. Defaults to 2,
    /// which can be used to calculate exponential back-off or other delay strategies.</param>
    /// <returns>An asynchronous retry policy configured according to the specified parameters.</returns>
    IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2);
}
