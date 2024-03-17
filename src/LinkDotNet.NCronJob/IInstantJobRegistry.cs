namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a registry for instant jobs.
/// </summary>
public interface IInstantJobRegistry
{
    /// <summary>
    /// Adds an instant job to the registry, which gets directly executed. The instance is retrieved from the container.
    /// </summary>
    void AddInstantJob<TJob>() where TJob : IJob;
}
