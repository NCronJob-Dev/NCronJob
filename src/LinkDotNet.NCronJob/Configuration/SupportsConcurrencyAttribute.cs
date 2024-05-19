namespace LinkDotNet.NCronJob;

/// <summary>
/// Specifies that multiple instances of the same job type can run simultaneously.
/// This attribute controls the maximum degree of parallelism allowed for instances of the job.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Delegate, Inherited = false)]
public sealed class SupportsConcurrencyAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of concurrent instances allowed for the job.
    /// </summary>
    public int MaxDegreeOfParallelism { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SupportsConcurrencyAttribute"/> class with the specified maximum degree of parallelism.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent instances that can run.</param>
    public SupportsConcurrencyAttribute(int maxDegreeOfParallelism) => MaxDegreeOfParallelism = maxDegreeOfParallelism;

    /// <inheritdoc />
    public SupportsConcurrencyAttribute() => MaxDegreeOfParallelism = Environment.ProcessorCount;
}

