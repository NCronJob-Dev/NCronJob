using System.Reflection;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents the metadata associated with a job, including retry and concurrency policies.
/// </summary>
/// <remarks>
/// The <see cref="JobExecutionAttributes"/> class is designed to encapsulate policy-related attributes that
/// affect how a job is executed. This includes handling retries and managing concurrency, which are
/// critical in environments where jobs need to be robust and efficient under varying system loads
/// and potential errors.
/// </remarks>
internal sealed class JobExecutionAttributes
{
    /// <summary>
    /// Gets the retry policy associated with the job, if any.
    /// </summary>
    /// <value>
    /// The <see cref="RetryPolicyAttribute"/> that defines the retry behavior for the job.
    /// This may be <c>null</c> if no retry policy has been explicitly defined.
    /// </value>
    public RetryPolicyAttribute? RetryPolicy { get; }

    /// <summary>
    /// Gets the concurrency policy associated with the job, if any.
    /// </summary>
    /// <value>
    /// The <see cref="SupportsConcurrencyAttribute"/> that defines the concurrency behavior for the job.
    /// This may be <c>null</c> if no concurrency policy has been explicitly defined.
    /// </value>
    public SupportsConcurrencyAttribute? ConcurrencyPolicy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutionAttributes"/> class using the specified job type or delegate.
    /// </summary>
    /// <param name="jobType">The type of the job. Used to retrieve job-specific attributes if <paramref name="jobDelegate"/> is <c>null</c>.</param>
    /// <param name="jobDelegate">The delegate representing the job. Used to retrieve attributes directly from the delegate if not <c>null</c>.</param>
    /// <remarks>
    /// If a <paramref name="jobDelegate"/> is provided, attributes will be retrieved from the method information of the delegate.
    /// Otherwise, attributes will be retrieved from the <paramref name="jobType"/>.
    /// This constructor allows for flexibility in defining job behavior either via type-level attributes or more dynamically via delegates.
    /// </remarks>
    public JobExecutionAttributes(Type jobType, Delegate? jobDelegate)
    {
        if (jobDelegate is not null)
        {
            var methodInfo = jobDelegate.Method;
            var retryPolicy = methodInfo.GetCustomAttribute<RetryPolicyAttribute>();
            var concurrencyPolicy = methodInfo.GetCustomAttribute<SupportsConcurrencyAttribute>();
            RetryPolicy = retryPolicy;
            ConcurrencyPolicy = concurrencyPolicy;
        }
        else
        {
            RetryPolicy = jobType.GetCustomAttribute<RetryPolicyAttribute>();
            ConcurrencyPolicy = jobType.GetCustomAttribute<SupportsConcurrencyAttribute>();
        }
    }
}
