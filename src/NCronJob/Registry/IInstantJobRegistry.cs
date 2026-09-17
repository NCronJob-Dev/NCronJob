using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    Guid RunScheduledJob(Type jobType, TimeSpan delay, object? parameter = null, CancellationToken token = default);

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
    Guid RunScheduledJob(string jobName, TimeSpan delay, object? parameter = null, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed at <paramref name="startDate"/>.
    /// </summary>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    [Obsolete("This method will be dropped in the next major version. Use RunScheduledJob<TJob>(TimeSpan, object?, CancellationToken) instead.")]
    Guid RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
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
    Guid RunScheduledJob(string jobName, DateTimeOffset startDate, object? parameter = null, CancellationToken token = default);

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
    Guid ForceRunScheduledJob(Type jobType, TimeSpan delay, object? parameter = null, CancellationToken token = default);

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
    Guid ForceRunScheduledJob(string jobName, TimeSpan delay, object? parameter = null, CancellationToken token = default);

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

internal interface IOptionalParameterInstantJobRegistry
{
    Guid RunWithOptionalParameter(
        Type jobType,
        TimeSpan delay,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token);

    Guid RunWithOptionalParameter(
        string jobName,
        TimeSpan delay,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token);

    Guid RunWithOptionalParameter(
        Type jobType,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        CancellationToken token);

    Guid RunWithOptionalParameter(
        string jobName,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        CancellationToken token);
}

#pragma warning disable S3060 // Compatibility dispatch preserves behavior for implementations compiled against the original interface.
internal static class InstantJobRegistryCompatibility
{
    public static Guid RunWithoutParameterOverride(
        IInstantJobRegistry registry,
        Type jobType,
        TimeSpan delay,
        bool forceExecution,
        CancellationToken token)
    {
        if (registry is IOptionalParameterInstantJobRegistry optionalParameterRegistry)
        {
            return optionalParameterRegistry.RunWithOptionalParameter(
                jobType,
                delay,
                OptionalParameter.Unspecified,
                forceExecution,
                token);
        }

        return forceExecution
            ? registry.ForceRunScheduledJob(jobType, delay, parameter: null, token)
            : registry.RunScheduledJob(jobType, delay, parameter: null, token);
    }

    public static Guid RunWithoutParameterOverride(
        IInstantJobRegistry registry,
        string jobName,
        TimeSpan delay,
        bool forceExecution,
        CancellationToken token)
    {
        if (registry is IOptionalParameterInstantJobRegistry optionalParameterRegistry)
        {
            return optionalParameterRegistry.RunWithOptionalParameter(
                jobName,
                delay,
                OptionalParameter.Unspecified,
                forceExecution,
                token);
        }

        return forceExecution
            ? registry.ForceRunScheduledJob(jobName, delay, parameter: null, token)
            : registry.RunScheduledJob(jobName, delay, parameter: null, token);
    }

    public static Guid RunWithoutParameterOverride<TJob>(
        IInstantJobRegistry registry,
        DateTimeOffset startDate,
        CancellationToken token)
        where TJob : IJob
    {
        if (registry is IOptionalParameterInstantJobRegistry optionalParameterRegistry)
        {
            return optionalParameterRegistry.RunWithOptionalParameter(
                typeof(TJob),
                startDate,
                OptionalParameter.Unspecified,
                token);
        }

#pragma warning disable CS0618
        return registry.RunScheduledJob<TJob>(startDate, parameter: null, token);
#pragma warning restore CS0618
    }

    public static Guid RunWithoutParameterOverride(
        IInstantJobRegistry registry,
        string jobName,
        DateTimeOffset startDate,
        CancellationToken token)
    {
        if (registry is IOptionalParameterInstantJobRegistry optionalParameterRegistry)
        {
            return optionalParameterRegistry.RunWithOptionalParameter(
                jobName,
                startDate,
                OptionalParameter.Unspecified,
                token);
        }

#pragma warning disable CS0618
        return registry.RunScheduledJob(jobName, startDate, parameter: null, token);
#pragma warning restore CS0618
    }
}
#pragma warning restore S3060

#pragma warning disable S4136 // Optional-parameter dispatch methods are kept beside the public overloads they implement.
internal sealed partial class InstantJobRegistry : IInstantJobRegistry, IOptionalParameterInstantJobRegistry
{
    private readonly TimeProvider timeProvider;
    private readonly JobQueueManager jobQueueManager;
    private readonly JobRegistry jobRegistry;
    private readonly JobWorker jobWorker;
    private readonly ConcurrencySettings settings;
    private readonly JobExecutionProgressObserver observer;
    private readonly ILogger<InstantJobRegistry> logger;

    public InstantJobRegistry(
        TimeProvider timeProvider,
        JobQueueManager jobQueueManager,
        JobRegistry jobRegistry,
        JobWorker jobWorker,
        ConcurrencySettings settings,
        JobExecutionProgressObserver observer,
        ILogger<InstantJobRegistry> logger)
    {
        this.timeProvider = timeProvider;
        this.jobQueueManager = jobQueueManager;
        this.jobRegistry = jobRegistry;
        this.jobWorker = jobWorker;
        this.settings = settings;
        this.observer = observer;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Guid RunScheduledJob(Type jobType, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobType, utcNow + delay, OptionalParameter.FromValue(parameter), false, token);
    }

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        Type jobType,
        TimeSpan delay,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobType, utcNow + delay, parameter, forceExecution, token);
    }

    /// <inheritdoc />
    public Guid RunScheduledJob(string jobName, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobName, utcNow + delay, OptionalParameter.FromValue(parameter), false, token);
    }

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        string jobName,
        TimeSpan delay,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobName, utcNow + delay, parameter, forceExecution, token);
    }

    /// <inheritdoc />
    public Guid RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
        where TJob : IJob =>
        RunJob(typeof(TJob), startDate, OptionalParameter.FromValue(parameter), false, token);

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        Type jobType,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        CancellationToken token)
        => RunJob(jobType, startDate, parameter, false, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(string jobName, DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
        => RunJob(jobName, startDate, OptionalParameter.FromValue(parameter), false, token);

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        string jobName,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        CancellationToken token)
        => RunJob(jobName, startDate, parameter, false, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunDelegateJob(jobDelegate, utcNow + delay, false, token);
    }

    /// <inheritdoc />
    public Guid RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, false, token);

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Type jobType, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobType, utcNow + delay, OptionalParameter.FromValue(parameter), true, token);
    }

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(string jobName, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobName, utcNow + delay, OptionalParameter.FromValue(parameter), true, token);
    }

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunDelegateJob(jobDelegate, utcNow + delay, true, token);
    }

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, true, token);

    private Guid RunDelegateJob(Delegate jobDelegate, DateTimeOffset startDate, bool forceExecution = false, CancellationToken token = default)
    {
        var jobDefinition = JobDefinition.CreateUntyped(null, jobDelegate);

        return RunInternal(jobDefinition, OptionalParameter.Unspecified, startDate, forceExecution, token);
    }

    private Guid RunJob(
        Type jobType,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token)
    {
        return RunJob(
            () => TypedJobFinder(jobType, parameter.Value),
            startDate,
            parameter,
            forceExecution,
            token);
    }

    private Guid RunJob(
        string jobName,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token)
    {
        return RunJob(
            () => NamedJobFinder(jobName),
            startDate,
            parameter,
            forceExecution,
            token);
    }

    private Guid RunJob(
        Func<JobDefinition> jobDefinitionFinder,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token)
    {
        using (logger.BeginScope("Triggering RunScheduledJob:"))
        {
            var jobDefinition = jobDefinitionFinder();

            return RunInternal(jobDefinition, parameter, startDate, forceExecution, token);
        }
    }

    private JobDefinition TypedJobFinder(Type jobType, object? parameter)
    {
        var jobDefinitions = jobRegistry.FindAllRootJobDefinition(jobType);

        if (jobDefinitions.Count > 1)
        {
            throw new InvalidOperationException(
                $"""
                Ambiguous job reference for type '{jobType.Name}' detected.
                """);
        }

        var jobDefinition = jobDefinitions.FirstOrDefault();

        if (jobDefinition is null)
        {
            LogJobNotRegistered(jobType.Name);
            jobDefinition = JobDefinition.CreateTyped(jobType, parameter);
        }

        return jobDefinition;
    }

    private JobDefinition NamedJobFinder(string jobName) => jobRegistry.FindRootJobDefinitionOrThrow(jobName);

    private Guid RunInternal(
        JobDefinition jobDefinition,
        OptionalParameter parameter,
        DateTimeOffset startDate,
        bool forceExecution,
        CancellationToken token)
    {
        var run = JobRun.CreateInstant(
            timeProvider,
            observer.Report,
            jobDefinition,
            startDate,
            parameter,
            token,
            settings);

        run.Priority = JobPriority.High;

        if (forceExecution)
        {
            _ = jobWorker.InvokeJob(run, token);
        }
        else
        {
            jobQueueManager.Enqueue(run);
        }

        return run.CorrelationId;
    }

    [LoggerMessage(LogLevel.Warning, "Job {JobName} is not registered, will create new registration.")]
    private partial void LogJobNotRegistered(string jobName);
}
#pragma warning restore S4136
