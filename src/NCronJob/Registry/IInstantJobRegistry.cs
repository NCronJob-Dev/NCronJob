using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NCronJob;

/// <summary>
/// Represents a registry for instant jobs.
/// </summary>
public interface IInstantJobRegistry
{
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
    [Obsolete("Use RunScheduledJob<TJob>(TimeSpan, object?, CancellationToken) instead.")]
    Guid RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
        where TJob : IJob;

    /// <summary>
    /// Runs a job that will be executed at <paramref name="startDate"/>.
    /// </summary>
    /// <param name="jobName">The name of the job to execute.</param>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    [Obsolete("Use RunScheduledJob(string, TimeSpan, object?, CancellationToken) instead.")]
    Guid RunScheduledJob(string jobName, DateTimeOffset startDate, object? parameter = null, CancellationToken token = default);

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
    [Obsolete("Use RunScheduledJob(Delegate, TimeSpan, CancellationToken) instead.")]
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
    [Obsolete("Use ForceRunScheduledJob(Delegate, TimeSpan, CancellationToken) instead.")]
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
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// The concurrency settings will be ignored.
    /// </summary>
    /// <param name="jobName">The name of the job to execute.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <returns>The job correlation id.</returns>
    Guid ForceRunScheduledJob(string jobName, TimeSpan delay, object? parameter = null, CancellationToken token = default);
}

internal sealed partial class InstantJobRegistry : IInstantJobRegistry
{
    private readonly TimeProvider timeProvider;
    private readonly JobQueueManager jobQueueManager;
    private readonly JobRegistry jobRegistry;
    private readonly JobWorker jobWorker;
    private readonly JobExecutionProgressObserver observer;
    private readonly ILogger<InstantJobRegistry> logger;

    public InstantJobRegistry(
        TimeProvider timeProvider,
        JobQueueManager jobQueueManager,
        JobRegistry jobRegistry,
        JobWorker jobWorker,
        JobExecutionProgressObserver observer,
        ILogger<InstantJobRegistry> logger)
    {
        this.timeProvider = timeProvider;
        this.jobQueueManager = jobQueueManager;
        this.jobRegistry = jobRegistry;
        this.jobWorker = jobWorker;
        this.observer = observer;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Guid RunScheduledJob(Type jobType, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobType, utcNow + delay, parameter, false, token);
    }

    /// <inheritdoc />
    public Guid RunScheduledJob(string jobName, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobName, utcNow + delay, parameter, false, token);
    }

    /// <inheritdoc />
    public Guid RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
        where TJob : IJob =>
        RunJob(typeof(TJob), startDate, parameter, false, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(string jobName, DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
        => RunJob(jobName, startDate, parameter, false, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunScheduledJob(jobDelegate, utcNow + delay, token);
    }

    /// <inheritdoc />
    public Guid RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, false, token);

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Type jobType, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobType, utcNow + delay, parameter, true, token);
    }

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(string jobName, TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return RunJob(jobName, utcNow + delay, parameter, true, token);
    }

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        return ForceRunScheduledJob(jobDelegate, utcNow + delay, token);
    }

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, true, token);

    private Guid RunDelegateJob(Delegate jobDelegate, DateTimeOffset startDate, bool forceExecution = false, CancellationToken token = default)
    {
        var jobDefinition = JobDefinition.CreateUntyped(null, jobDelegate);

        return RunInternal(jobDefinition, null, startDate, forceExecution, token);
    }

    private Guid RunJob(Type jobType, DateTimeOffset startDate, object? parameter = null, bool forceExecution = false, CancellationToken token = default)
    {
        return RunJob(
            () => TypedJobfinder(jobType, parameter),
            startDate,
            parameter,
            forceExecution,
            token);
    }

    private Guid RunJob(string jobName, DateTimeOffset startDate, object? parameter = null, bool forceExecution = false, CancellationToken token = default)
    {
        return RunJob(
            () => NamedJobfinder(jobName),
            startDate,
            parameter,
            forceExecution,
            token);
    }

    private Guid RunJob(
        Func<JobDefinition> jobDefinitionFinder,
        DateTimeOffset startDate,
        object? parameter = null,
        bool forceExecution = false,
        CancellationToken token = default)
    {
        using (logger.BeginScope("Triggering RunScheduledJob:"))
        {
            var jobDefinition = jobDefinitionFinder();

            token.Register(() => LogCancellationRequested(parameter));

            return RunInternal(jobDefinition, parameter, startDate, forceExecution, token);
        }
    }

    private JobDefinition TypedJobfinder(Type jobType, object? parameter)
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

    private JobDefinition NamedJobfinder(string jobName)
    {
        var jobDefinition = jobRegistry.FindRootJobDefinition(jobName);

        if (jobDefinition is null)
        {
            throw new InvalidOperationException($"Job with name '{jobName}' not found.");
        }

        return jobDefinition;
    }

    private Guid RunInternal(
        JobDefinition jobDefinition,
        object? parameter,
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
            token);

        run.Priority = JobPriority.High;

        if (forceExecution)
        {
            _ = jobWorker.InvokeJob(run, token);
        }
        else
        {
            var jobQueue = jobQueueManager.GetOrAddQueue(run.JobDefinition.JobFullName);
            jobQueue.EnqueueForDirectExecution(run);
            jobQueueManager.SignalJobQueue(run.JobDefinition.JobFullName);
        }

        return run.CorrelationId;
    }

    [LoggerMessage(LogLevel.Warning, "Job {JobName} cancelled by request.")]
    private partial void LogCancellationNotice(string jobName);

    [LoggerMessage(LogLevel.Debug, "Cancellation requested for CronRegistry {Parameter}.")]
    private partial void LogCancellationRequested(object? parameter);

    [LoggerMessage(LogLevel.Warning, "Job {JobName} is not registered, will create new registration.")]
    private partial void LogJobNotRegistered(string jobName);
}
