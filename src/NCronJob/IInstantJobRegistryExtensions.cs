namespace NCronJob;

/// <summary>
/// Extensions for InstantJobRegistry.
/// </summary>
public static class IInstantJobRegistryExtensions
{
    /// <summary>
    /// Queues an instant job to the JobQueue. The instance is retrieved from the container.
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// </summary>
    /// <returns>The job correlation id.</returns>
    /// <remarks>
    /// This is a fire-and-forget process, the Job will be queued with high priority and run in the background. The contents of <paramref name="parameter" />
    /// are not serialized and deserialized. It is the reference to the <paramref name="parameter"/>-object that gets passed in.
    /// </remarks>
    /// <example>
    /// Running a job with a parameter:
    /// <code>
    /// instantJobRegistry.RunInstantJob&lt;MyJob&gt;(new MyParameterObject { Foo = "Bar" });
    /// </code>
    /// </example>
    public static Guid RunInstantJob<TJob>(
        this IInstantJobRegistry instantJobRegistry,
        object? parameter = null,
        CancellationToken token = default)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.RunInstantJob(typeof(TJob), parameter, token);
    }

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>.
    /// </summary>
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    public static Guid RunScheduledJob<TJob>(
        this IInstantJobRegistry instantJobRegistry,
        TimeSpan delay,
        object? parameter = null,
        CancellationToken token = default)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.RunScheduledJob(typeof(TJob), delay, parameter, token);
    }

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// The concurrency settings will be ignored.
    /// </summary>
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    public static Guid ForceRunScheduledJob<TJob>(
        this IInstantJobRegistry instantJobRegistry,
        TimeSpan delay,
        object? parameter = null,
        CancellationToken token = default)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.ForceRunScheduledJob(typeof(TJob), delay, parameter, token);
    }

    /// <summary>
    /// Runs an instant job to the registry, which will be executed even if the job is not registered and the concurrency is exceeded.
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// </summary>
    /// <returns>The job correlation id.</returns>
    /// <remarks>
    /// This is a fire-and-forget process, the Job will be run immediately in the background. The contents of <paramref name="parameter" />
    /// are not serialized and deserialized. It is the reference to the <paramref name="parameter"/>-object that gets passed in.
    /// </remarks>
    /// <example>
    /// Running a job with a parameter:
    /// <code>
    /// instantJobRegistry.ForceRunInstantJob&lt;MyJob&gt;(new MyParameterObject { Foo = "Bar" });
    /// </code>
    /// </example>
    public static Guid ForceRunInstantJob<TJob>(
        this IInstantJobRegistry instantJobRegistry,
        object? parameter = null,
        CancellationToken token = default)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.ForceRunInstantJob(typeof(TJob), parameter, token);
    }
}
