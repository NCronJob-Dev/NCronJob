namespace NCronJob;

/// <summary>
/// Represents the configuration settings for managing concurrency within the application.
/// </summary>
/// <remarks>
/// This configuration is utilized to specify the maximum number of concurrent operations
/// that the system can execute simultaneously.
/// </remarks>
internal sealed class ConcurrencySettings
{
    /// <summary>
    /// The total number of concurrent jobs that can be executed
    /// by the scheduler at any one time, irrespective of the job type.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount * 4;

    /// <summary>
    /// The default time a scheduled job may remain queued after its intended run time.
    /// </summary>
    public TimeSpan DefaultJobRunExpiry { get; set; } = TimeSpan.FromMinutes(10);

    public ConcurrencySettings Snapshot() => new()
    {
        MaxDegreeOfParallelism = MaxDegreeOfParallelism,
        DefaultJobRunExpiry = DefaultJobRunExpiry,
    };

    public void Restore(ConcurrencySettings snapshot)
    {
        MaxDegreeOfParallelism = snapshot.MaxDegreeOfParallelism;
        DefaultJobRunExpiry = snapshot.DefaultJobRunExpiry;
    }
}
