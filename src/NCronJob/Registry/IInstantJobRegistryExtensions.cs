﻿using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Extensions for IInstantJobRegistry.
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
    /// Queues an instant job to the JobQueue. The instance is retrieved from the container.
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="jobType">The job type. Expected to implement the <see cref="IJob"/> interface.</param>
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
    /// instantJobRegistry.RunInstantJob(typeof(MyJob), new MyParameterObject { Foo = "Bar" });
    /// </code>
    /// </example>
    public static Guid RunInstantJob(
        this IInstantJobRegistry instantJobRegistry,
        Type jobType,
        object? parameter = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.RunScheduledJob(jobType, TimeSpan.Zero, parameter, token);
    }

    /// <summary>
    /// Runs an instant job, which gets directly executed.
    /// </summary>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="NCronJobExtensions.AddNCronJob(IServiceCollection, Delegate, string, TimeZoneInfo)"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    public static Guid RunInstantJob(
        this IInstantJobRegistry instantJobRegistry,
        Delegate jobDelegate,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.RunScheduledJob(jobDelegate, TimeSpan.Zero, token);
    }

    /// <summary>
    /// Runs an instant job, which gets directly executed.
    /// </summary>
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="jobName">The name of the job to execute.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    public static Guid RunInstantJob(
        this IInstantJobRegistry instantJobRegistry,
        string jobName,
        object? parameter = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.RunScheduledJob(jobName, TimeSpan.Zero, parameter, token);
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

    /// <summary>
    /// Runs an instant job to the registry, which will be executed even if the job is not registered and the concurrency is exceeded.
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="jobType">The job type. Expected to implement the <see cref="IJob"/> interface.</param>
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
    /// instantJobRegistry.ForceRunInstantJob(typeof(MyJob), new MyParameterObject { Foo = "Bar" });
    /// </code>
    /// </example>
    public static Guid ForceRunInstantJob(
        this IInstantJobRegistry instantJobRegistry,
        Type jobType,
        object? parameter = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.ForceRunScheduledJob(jobType, TimeSpan.Zero, parameter, token);
    }

    /// <summary>
    /// Runs an instant job to the registry, which will be executed even if the job is not registered and the concurrency is exceeded.
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// <param name="jobName">The name of the job to execute.</param>
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
    /// instantJobRegistry.RunInstantJob("my_job", new MyParameterObject { Foo = "Bar" });
    /// </code>
    /// </example>
    public static Guid ForceRunInstantJob(
        this IInstantJobRegistry instantJobRegistry,
        string jobName,
        object? parameter = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.ForceRunScheduledJob(jobName, TimeSpan.Zero, parameter, token);
    }

    /// <summary>
    /// Runs an instant job, which gets directly executed. The job will not be queued into the JobQueue, but executed directly.
    /// <param name="instantJobRegistry">The instant job registry.</param>
    /// </summary>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="NCronJobExtensions.AddNCronJob(IServiceCollection, Delegate, string, TimeZoneInfo)"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    public static Guid ForceRunInstantJob(
        this IInstantJobRegistry instantJobRegistry,
        Delegate jobDelegate,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(instantJobRegistry);

        return instantJobRegistry.ForceRunScheduledJob(jobDelegate, TimeSpan.Zero, token);
    }
}
