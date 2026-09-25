using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Represents a registry for instant jobs.
/// </summary>
public interface IInstantJobRegistry
{
    /// <summary>
    /// Runs a job without supplying a parameter override after the given <paramref name="delay"/>.
    /// </summary>
    /// <remarks>
    /// The built-in registry uses the configured job parameter. For compatibility, implementations compiled against
    /// earlier versions receive <c>null</c> through the existing parameter overload.
    /// </remarks>
    Guid RunScheduledJob(Type jobType, TimeSpan delay, CancellationToken token = default)
        => InstantJobRegistryCompatibility.RunWithoutParameterOverride(this, jobType, delay, forceExecution: false, token);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>.
    /// </summary>
    /// <param name="jobType">The job type. Expected to implement the <see cref="IJob"/> interface.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    Guid RunScheduledJob(Type jobType, TimeSpan delay, object? parameter, CancellationToken token = default);

    /// <summary>
    /// Runs a named job without supplying a parameter override after the given <paramref name="delay"/>.
    /// </summary>
    /// <remarks>
    /// The built-in registry uses the configured job parameter. For compatibility, implementations compiled against
    /// earlier versions receive <c>null</c> through the existing parameter overload.
    /// </remarks>
    Guid RunScheduledJob(string jobName, TimeSpan delay, CancellationToken token = default)
        => InstantJobRegistryCompatibility.RunWithoutParameterOverride(this, jobName, delay, forceExecution: false, token);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>.
    /// </summary>
    /// <param name="jobName">The name of the job to execute.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    Guid RunScheduledJob(string jobName, TimeSpan delay, object? parameter, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed at <paramref name="startDate"/>.
    /// </summary>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    [Obsolete("This method will be dropped in the next major version. Use RunScheduledJob<TJob>(TimeSpan, object?, CancellationToken) instead.")]
    Guid RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter, CancellationToken token = default)
        where TJob : IJob;

    /// <summary>
    /// Runs a job without supplying a parameter override at <paramref name="startDate"/>.
    /// </summary>
    /// <remarks>
    /// The built-in registry uses the configured job parameter. For compatibility, implementations compiled against
    /// earlier versions receive <c>null</c> through the existing parameter overload.
    /// </remarks>
    [Obsolete("This method will be dropped in the next major version. Use RunScheduledJob<TJob>(TimeSpan, CancellationToken) instead.")]
    Guid RunScheduledJob<TJob>(DateTimeOffset startDate, CancellationToken token = default)
        where TJob : IJob
        => InstantJobRegistryCompatibility.RunWithoutParameterOverride<TJob>(this, startDate, token);

    /// <summary>
    /// Runs a job that will be executed at <paramref name="startDate"/>.
    /// </summary>
    /// <param name="jobName">The name of the job to execute.</param>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    [Obsolete("This method will be dropped in the next major version. Use RunScheduledJob(string, TimeSpan, object?, CancellationToken) instead.")]
    Guid RunScheduledJob(string jobName, DateTimeOffset startDate, object? parameter, CancellationToken token = default);

    /// <summary>
    /// Runs a named job without supplying a parameter override at <paramref name="startDate"/>.
    /// </summary>
    /// <remarks>
    /// The built-in registry uses the configured job parameter. For compatibility, implementations compiled against
    /// earlier versions receive <c>null</c> through the existing parameter overload.
    /// </remarks>
    [Obsolete("This method will be dropped in the next major version. Use RunScheduledJob(string, TimeSpan, CancellationToken) instead.")]
    Guid RunScheduledJob(string jobName, DateTimeOffset startDate, CancellationToken token = default)
        => InstantJobRegistryCompatibility.RunWithoutParameterOverride(this, jobName, startDate, token);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="NCronJobExtensions.AddNCronJob(IServiceCollection, Delegate, string, TimeZoneInfo)"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    Guid RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed at the given <paramref name="startDate"/>.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="NCronJobExtensions.AddNCronJob(IServiceCollection, Delegate, string, TimeZoneInfo)"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    [Obsolete("This method will be dropped in the next major version. Use RunScheduledJob(Delegate, TimeSpan, CancellationToken) instead.")]
    Guid RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="NCronJobExtensions.AddNCronJob(IServiceCollection, Delegate, string, TimeZoneInfo)"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    Guid ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed at the given <paramref name="startDate"/>. The job will not be queued into the JobQueue, but executed directly.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="NCronJobExtensions.AddNCronJob(IServiceCollection, Delegate, string, TimeZoneInfo)"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    [Obsolete("This method will be dropped in the next major version. Use ForceRunScheduledJob(Delegate, TimeSpan, CancellationToken) instead.")]
    Guid ForceRunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// The concurrency settings will be ignored.
    /// </summary>
    /// <param name="jobType">The job type. Expected to implement the <see cref="IJob"/> interface.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    Guid ForceRunScheduledJob(Type jobType, TimeSpan delay, object? parameter, CancellationToken token = default);

    /// <summary>
    /// Runs a job without supplying a parameter override after the given <paramref name="delay"/>, ignoring concurrency settings.
    /// </summary>
    /// <remarks>
    /// The built-in registry uses the configured job parameter. For compatibility, implementations compiled against
    /// earlier versions receive <c>null</c> through the existing parameter overload.
    /// </remarks>
    Guid ForceRunScheduledJob(Type jobType, TimeSpan delay, CancellationToken token = default)
        => InstantJobRegistryCompatibility.RunWithoutParameterOverride(this, jobType, delay, forceExecution: true, token);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// The concurrency settings will be ignored.
    /// </summary>
    /// <param name="jobName">The name of the job to execute.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    Guid ForceRunScheduledJob(string jobName, TimeSpan delay, object? parameter, CancellationToken token = default);

    /// <summary>
    /// Runs a named job without supplying a parameter override after the given <paramref name="delay"/>, ignoring concurrency settings.
    /// </summary>
    /// <remarks>
    /// The built-in registry uses the configured job parameter. For compatibility, implementations compiled against
    /// earlier versions receive <c>null</c> through the existing parameter overload.
    /// </remarks>
    Guid ForceRunScheduledJob(string jobName, TimeSpan delay, CancellationToken token = default)
        => InstantJobRegistryCompatibility.RunWithoutParameterOverride(this, jobName, delay, forceExecution: true, token);
}
