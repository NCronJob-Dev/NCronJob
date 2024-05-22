using System.Reflection;

namespace NCronJob;

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
    /// <param name="jobDelegate">The delegate representing the job. Used to retrieve attributes directly from the delegate if not <c>null</c>.</param>
    /// <remarks>
    /// Attributes will be retrieved from the method information of the delegate.
    /// </remarks>
    public JobExecutionAttributes(Delegate jobDelegate)
    {
        var cachedAttributes = JobAttributeCache.GetJobExecutionAttributes(jobDelegate.Method);
        RetryPolicy = cachedAttributes.RetryPolicy;
        ConcurrencyPolicy = cachedAttributes.ConcurrencyPolicy;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutionAttributes"/> class using the specified job type or delegate.
    /// </summary>
    /// <param name="jobType">The type of the job. Used to retrieve job-specific attributes.</param>
    /// <remarks>
    /// Attributes will be retrieved from the <paramref name="jobType"/>.
    /// </remarks>
    public JobExecutionAttributes(Type jobType)
    {
        var cachedAttributes = JobAttributeCache.GetJobExecutionAttributes(jobType);
        RetryPolicy = cachedAttributes.RetryPolicy;
        ConcurrencyPolicy = cachedAttributes.ConcurrencyPolicy;
    }

    private JobExecutionAttributes(RetryPolicyAttribute? retryPolicy, SupportsConcurrencyAttribute? concurrencyPolicy)
    {
        RetryPolicy = retryPolicy;
        ConcurrencyPolicy = concurrencyPolicy;
    }

    // Internal factory methods for cache usage
    internal static JobExecutionAttributes CreateFromType(Type jobType)
    {
        var retryPolicy = jobType.GetCustomAttribute<RetryPolicyAttribute>();
        var concurrencyPolicy = jobType.GetCustomAttribute<SupportsConcurrencyAttribute>();
        return new JobExecutionAttributes(retryPolicy, concurrencyPolicy);
    }

    internal static JobExecutionAttributes CreateFromMethodInfo(MethodInfo methodInfo)
    {
        var retryPolicy = methodInfo.GetCustomAttribute<RetryPolicyAttribute>();
        var concurrencyPolicy = methodInfo?.GetCustomAttribute<SupportsConcurrencyAttribute>();
        return new JobExecutionAttributes(retryPolicy, concurrencyPolicy);
    }
}
