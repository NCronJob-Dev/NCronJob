namespace NCronJob;

/// <summary>
/// Extensions for IRuntimeJobRegistry.
/// </summary>
public static class IRuntimeJobRegistryExtensions
{
    /// <summary>
    /// Tries to register a job with the given configuration.
    /// </summary>/param>
    /// <param name="runtimeJobRegistry"> The runtime job registry.</param>
    /// <param name="jobBuilder">The job builder that configures the job.</param>
    /// <returns>Returns <c>true</c> if the registration was successful, otherwise <c>false</c>.</returns>
    public static bool TryRegister(
        this IRuntimeJobRegistry runtimeJobRegistry,
        Action<IRuntimeJobBuilder> jobBuilder)
    {
        ArgumentNullException.ThrowIfNull(runtimeJobRegistry);

        return runtimeJobRegistry.TryRegister(jobBuilder, out _);
    }

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
