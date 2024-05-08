using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

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
    void RunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed at <paramref name="startDate"/>. If it is in the past, the job will be executed immediately.
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
    private readonly ILogger<InstantJobRegistry> logger;

    public InstantJobRegistry(TimeProvider timeProvider, JobQueue jobQueue,  ILogger<InstantJobRegistry> logger)
    {
        this.timeProvider = timeProvider;
        this.jobQueue = jobQueue;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob => RunScheduledJob<TJob>(TimeSpan.Zero, parameter, token);

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        token.Register(() => LogCancellationRequested(parameter));

        var run = new JobDefinition(typeof(TJob), parameter, null, null, null) { CancellationToken = token };

        jobQueue.EnqueueForDirectExecution(run);
    }

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default) where TJob : IJob
    {
        var utcNow = timeProvider.GetUtcNow();
        ArgumentOutOfRangeException.ThrowIfLessThan(startDate, utcNow);

        var delay = startDate - utcNow;
        RunScheduledJob<TJob>(delay, parameter, token);
    }

    [LoggerMessage(LogLevel.Warning, "Job {JobName} cancelled by request.")]
    private partial void LogCancellationNotice(string jobName);

    [LoggerMessage(LogLevel.Debug, "Cancellation requested for CronRegistry {Parameter}.")]
    private partial void LogCancellationRequested(object? parameter);
}
