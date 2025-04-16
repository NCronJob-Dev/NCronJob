namespace NCronJob;

/// <summary>
/// Extensions for IRuntimeJobRegistry.
/// </summary>
public static class IRuntimeJobRegistryExtensions
{
    /// <summary>
    /// Removes all jobs of the given type.
    /// </summary>
    /// <remarks>If the given job is not found, no exception is thrown.</remarks>
    public static void RemoveJob<TJob>(this IRuntimeJobRegistry runtimeJobRegistry)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(runtimeJobRegistry);

        runtimeJobRegistry.RemoveJob(typeof(TJob));
    }

    /// <summary>
    /// Enables all jobs of the given type that were previously disabled.
    /// </summary>
    /// <remarks>
    /// If the job is already enabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    public static void EnableJob<TJob>(this IRuntimeJobRegistry runtimeJobRegistry)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(runtimeJobRegistry);
        
        runtimeJobRegistry.EnableJob(typeof(TJob));
    }

    /// <summary>
    /// Disables all jobs of the given type.
    /// </summary>
    /// <remarks>
    /// If the job is already disabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    public static void DisableJob<TJob>(this IRuntimeJobRegistry runtimeJobRegistry) 
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(runtimeJobRegistry);

        runtimeJobRegistry.DisableJob(typeof(TJob));
    }
}
