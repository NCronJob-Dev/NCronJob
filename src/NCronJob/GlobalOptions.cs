namespace NCronJob;

/// <summary>
/// The global options object to configure NCronJob.
/// </summary>
public sealed class GlobalOptions
{
    /// <summary>
    /// Gets or sets the global maximum degree of parallelism allowed for all jobs. That is, how many jobs can run in parallel.
    /// </summary>
    /// <remarks>
    /// The default value is the processor count multiplied by 4.
    /// </remarks>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount * 4;

    internal ConcurrencySettings ToConcurrencySettings() => new()
    {
        MaxDegreeOfParallelism = MaxDegreeOfParallelism
    };
}
