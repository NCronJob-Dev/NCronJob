using Microsoft.Extensions.Logging;

namespace NCronJob;

/// <summary>
/// Represents a registry for instant jobs.
/// </summary>
public interface IInstantJobRegistry
{
    /// <summary>
    /// Runs an instant job to the registry, which gets directly executed. The instance is retrieved from the container.
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// </summary>
    /// <remarks>
    /// This is a fire-and-forget process, the Job will be run in the background. The contents of <paramref name="parameter" />
    /// are not serialized and deserialized. It is the reference to the <paramref name="parameter"/>-object that gets passed in.
    /// </remarks>
    /// <example>
    /// Running a job with a parameter:
    /// <code>
    /// instantJobRegistry.RunInstantJob&lt;MyJob&gt;(new MyParameterObject { Foo = "Bar" });
    /// </code>
    /// </example>
    void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob;

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>.
    /// </summary>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    void RunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
        where TJob : IJob;

    /// <summary>
    /// Runs a job that will be executed at <paramref name="startDate"/>.
    /// </summary>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    void RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
        where TJob : IJob;
}

internal sealed partial class InstantJobRegistry : IInstantJobRegistry
{
    private readonly TimeProvider timeProvider;
    private readonly JobQueue jobQueue;
    private readonly JobRegistry jobRegistry;
    private readonly ILogger<InstantJobRegistry> logger;

    public InstantJobRegistry(
        TimeProvider timeProvider,
        JobQueue jobQueue,
        JobRegistry jobRegistry,
        ILogger<InstantJobRegistry> logger)
    {
        this.timeProvider = timeProvider;
        this.jobQueue = jobQueue;
        this.jobRegistry = jobRegistry;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob => RunScheduledJob<TJob>(TimeSpan.Zero, parameter, token);

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
        where TJob : IJob
    {
        var utcNow = timeProvider.GetUtcNow();
        RunScheduledJob<TJob>(utcNow + delay, parameter, token);
    }

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default) where TJob : IJob
    {
        if (!jobRegistry.IsJobRegistered<TJob>())
        {
            LogJobNotRegistered(typeof(TJob).Name);
            throw new InvalidOperationException($"Job {typeof(TJob).Name} is not registered.");
        }

        token.Register(() => LogCancellationRequested(parameter));

        var run = new JobDefinition(typeof(TJob), parameter, null, null, Priority: JobPriority.High)
        {
            CancellationToken = token,
            RunAt = startDate
        };

        jobQueue.EnqueueForDirectExecution(run, startDate);
    }

    [LoggerMessage(LogLevel.Warning, "Job {JobName} cancelled by request.")]
    private partial void LogCancellationNotice(string jobName);

    [LoggerMessage(LogLevel.Debug, "Cancellation requested for CronRegistry {Parameter}.")]
    private partial void LogCancellationRequested(object? parameter);

    [LoggerMessage(LogLevel.Error, "Job {JobName} is not registered.")]
    private partial void LogJobNotRegistered(string jobName);
}
