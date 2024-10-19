using Microsoft.Extensions.Logging;

namespace NCronJob;

/// <summary>
/// Represents a registry for instant jobs.
/// </summary>
public interface IInstantJobRegistry
{
    /// <summary>
    /// Queues an instant job to the JobQueue. The instance is retrieved from the container.
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// </summary>
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
    void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob;

    /// <summary>
    /// Runs an instant job, which gets directly executed.
    /// </summary>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    void RunInstantJob(Delegate jobDelegate, CancellationToken token = default);

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

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    void RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed at the given <paramref name="startDate"/>.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    void RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    void ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed at the given <paramref name="startDate"/>. The job will not be queued into the JobQueue, but executed directly.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    void ForceRunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default);

    /// <summary>
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// The concurrency settings will be ignored.
    /// </summary>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    void ForceRunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
        where TJob : IJob;

    /// <summary>
    /// Runs an instant job to the registry, which will be executed even if the job is not registered and the concurrency is exceeded.
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    /// </summary>
    /// <remarks>
    /// This is a fire-and-forget process, the Job will be run immediately in the background. The contents of <paramref name="parameter" />
    /// are not serialized and deserialized. It is the reference to the <paramref name="parameter"/>-object that gets passed in.
    /// </remarks>
    /// <example>
    /// Running a job with a parameter:
    /// <code>
    /// instantJobRegistry.RunInstantJob&lt;MyJob&gt;(new MyParameterObject { Foo = "Bar" });
    /// </code>
    /// </example>
    void ForceRunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob;

    /// <summary>
    /// Runs an instant job, which gets directly executed. The job will not be queued into the JobQueue, but executed directly.
    /// </summary>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    void ForceRunInstantJob(Delegate jobDelegate, CancellationToken token = default);

}

internal sealed partial class InstantJobRegistry : IInstantJobRegistry
{
    private readonly TimeProvider timeProvider;
    private readonly JobQueueManager jobQueueManager;
    private readonly JobRegistry jobRegistry;
    private readonly DynamicJobFactoryRegistry dynamicJobFactoryRegistry;
    private readonly JobWorker jobWorker;
    private readonly ILogger<InstantJobRegistry> logger;

    public InstantJobRegistry(
        TimeProvider timeProvider,
        JobQueueManager jobQueueManager,
        JobRegistry jobRegistry,
        DynamicJobFactoryRegistry dynamicJobFactoryRegistry,
        JobWorker jobWorker,
        ILogger<InstantJobRegistry> logger)
    {
        this.timeProvider = timeProvider;
        this.jobQueueManager = jobQueueManager;
        this.jobRegistry = jobRegistry;
        this.dynamicJobFactoryRegistry = dynamicJobFactoryRegistry;
        this.jobWorker = jobWorker;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob => RunScheduledJob<TJob>(TimeSpan.Zero, parameter, token);

    /// <inheritdoc />
    public void RunInstantJob(Delegate jobDelegate, CancellationToken token = default) => RunScheduledJob(jobDelegate, TimeSpan.Zero, token);

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
        where TJob : IJob
    {
        var utcNow = timeProvider.GetUtcNow();
        RunJob<TJob>(utcNow + delay, parameter, false, token);
    }

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default)
        where TJob : IJob =>
        RunJob<TJob>(startDate, parameter, false, token);

    /// <inheritdoc />
    public void RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        RunScheduledJob(jobDelegate, utcNow + delay, token);
    }

    /// <inheritdoc />
    public void RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, false, token);

    /// <inheritdoc />
    public void ForceRunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
        where TJob : IJob
    {
        var utcNow = timeProvider.GetUtcNow();
        RunJob<TJob>(utcNow + delay, parameter, true, token);
    }

    /// <inheritdoc />
    public void ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        ForceRunScheduledJob(jobDelegate, utcNow + delay, token);
    }

    /// <inheritdoc />
    public void ForceRunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
        RunDelegateJob(jobDelegate, startDate, true, token);

    /// <inheritdoc />
    public void ForceRunInstantJob(Delegate jobDelegate, CancellationToken token = default) =>
        ForceRunScheduledJob(jobDelegate, TimeSpan.Zero, token);

    /// <inheritdoc />
    public void ForceRunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob => ForceRunScheduledJob<TJob>(TimeSpan.Zero, parameter, token);

    private void RunDelegateJob(Delegate jobDelegate, DateTimeOffset startDate, bool forceExecution = false, CancellationToken token = default)
    {
        var definition = dynamicJobFactoryRegistry.Add(jobDelegate);
        var run = JobRun.Create(definition);
        run.Priority = JobPriority.High;
        run.RunAt = startDate;
        run.IsOneTimeJob = true;
        run.CancellationToken = token;

        if (forceExecution)
        {
            _ = jobWorker.InvokeJobWithSchedule(run, token);
        }
        else
        {
            var jobQueue = jobQueueManager.GetOrAddQueue(run.JobDefinition.JobFullName);
            jobQueue.EnqueueForDirectExecution(run, startDate);
            jobQueueManager.SignalJobQueue(run.JobDefinition.JobFullName);
        }
    }

    private void RunJob<TJob>(DateTimeOffset startDate, object? parameter = null, bool forceExecution = false, CancellationToken token = default)
        where TJob : IJob
    {
        using (logger.BeginScope("Triggering RunScheduledJob:"))
        {
            var newJobDefinition = new JobDefinition(typeof(TJob), parameter, null, null);

            if (!jobRegistry.IsJobRegistered<TJob>())
            {
                LogJobNotRegistered(typeof(TJob).Name);
                jobRegistry.Add(newJobDefinition);
            }

            token.Register(() => LogCancellationRequested(parameter));

            var run = JobRun.Create(newJobDefinition, parameter, token);
            run.Priority = JobPriority.High;
            run.RunAt = startDate;
            run.IsOneTimeJob = true;

            if (forceExecution)
            {
                _ = jobWorker.InvokeJobWithSchedule(run, token);
            }
            else
            {
                var jobQueue = jobQueueManager.GetOrAddQueue(run.JobDefinition.JobFullName);
                jobQueue.EnqueueForDirectExecution(run, startDate);
                jobQueueManager.SignalJobQueue(run.JobDefinition.JobFullName);
            }
        }
    }

    [LoggerMessage(LogLevel.Warning, "Job {JobName} cancelled by request.")]
    private partial void LogCancellationNotice(string jobName);

    [LoggerMessage(LogLevel.Debug, "Cancellation requested for CronRegistry {Parameter}.")]
    private partial void LogCancellationRequested(object? parameter);

    [LoggerMessage(LogLevel.Warning, "Job {JobName} is not registered, will create new registration.")]
    private partial void LogJobNotRegistered(string jobName);
}
