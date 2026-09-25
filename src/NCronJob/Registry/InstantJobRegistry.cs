using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NCronJob;

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
    public Guid RunScheduledJob(Type jobType, TimeSpan delay, object? parameter, CancellationToken token = default) =>
        RunJob(jobType, At(delay), OptionalParameter.FromValue(parameter), false, token);

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        Type jobType,
        TimeSpan delay,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token) =>
        RunJob(jobType, At(delay), parameter, forceExecution, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(string jobName, TimeSpan delay, object? parameter, CancellationToken token = default) =>
        RunJob(jobName, At(delay), OptionalParameter.FromValue(parameter), false, token);

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        string jobName,
        TimeSpan delay,
        OptionalParameter parameter,
        bool forceExecution,
        CancellationToken token) =>
        RunJob(jobName, At(delay), parameter, forceExecution, token);

    /// <inheritdoc />
    public Guid RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter, CancellationToken token = default)
        where TJob : IJob =>
        RunJob(typeof(TJob), startDate, OptionalParameter.FromValue(parameter), false, token);

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        Type jobType,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        CancellationToken token)
        => RunJob(jobType, startDate, parameter, false, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(string jobName, DateTimeOffset startDate, object? parameter, CancellationToken token = default)
        => RunJob(jobName, startDate, OptionalParameter.FromValue(parameter), false, token);

    Guid IOptionalParameterInstantJobRegistry.RunWithOptionalParameter(
        string jobName,
        DateTimeOffset startDate,
        OptionalParameter parameter,
        CancellationToken token)
        => RunJob(jobName, startDate, parameter, false, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, At(delay), false, token);

    /// <inheritdoc />
    public Guid RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, false, token);

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Type jobType, TimeSpan delay, object? parameter, CancellationToken token = default) =>
        RunJob(jobType, At(delay), OptionalParameter.FromValue(parameter), true, token);

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(string jobName, TimeSpan delay, object? parameter, CancellationToken token = default) =>
        RunJob(jobName, At(delay), OptionalParameter.FromValue(parameter), true, token);

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, At(delay), true, token);

    /// <inheritdoc />
    public Guid ForceRunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, true, token);

    private DateTimeOffset At(TimeSpan delay) => timeProvider.GetUtcNow() + delay;

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
            _ = jobWorker.RunImmediatelyAsync(run, token);
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
