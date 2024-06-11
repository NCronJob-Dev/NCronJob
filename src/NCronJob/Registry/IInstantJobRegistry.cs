using Microsoft.Extensions.DependencyInjection;
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
    void RunInstantJob(Delegate jobDelegate);

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
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    void RunScheduledJob(Delegate jobDelegate, TimeSpan delay);

    /// <summary>
    /// Runs a job that will be executed at the given <paramref name="startDate"/>.
    /// </summary>
    /// <param name="jobDelegate">The delegate to execute.</param>
    /// <param name="startDate">The starting point when the job will be executed.</param>
    /// <remarks>
    /// The <paramref name="jobDelegate"/> delegate supports, like <see cref="ServiceCollectionExtensions.AddNCronJob"/>, that services can be retrieved dynamically.
    /// Also, the <see cref="CancellationToken"/> can be retrieved in this way.
    /// </remarks>
    void RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate);

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
    /// Runs a job that will be executed after the given <paramref name="delay"/>. The job will not be queued into the JobQueue, but executed directly.
    /// The concurrency settings will be ignored.
    /// </summary>
    /// <param name="delay">The delay until the job will be executed.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// <param name="token">An optional token to cancel the job.</param>
    void ForceRunInstantJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
        where TJob : IJob;
}

internal sealed partial class InstantJobRegistry : IInstantJobRegistry
{
    private readonly TimeProvider timeProvider;
    private readonly JobQueueManager jobQueueManager;
    private readonly JobRegistry jobRegistry;
    private readonly DynamicJobFactoryRegistry dynamicJobFactoryRegistry;
    private readonly JobProcessor jobProcessor;
    private readonly ILogger<InstantJobRegistry> logger;

    public InstantJobRegistry(
        TimeProvider timeProvider,
        JobQueueManager jobQueueManager,
        JobRegistry jobRegistry,
        DynamicJobFactoryRegistry dynamicJobFactoryRegistry,
        JobProcessor jobProcessor,
        ILogger<InstantJobRegistry> logger)
    {
        this.timeProvider = timeProvider;
        this.jobQueueManager = jobQueueManager;
        this.jobRegistry = jobRegistry;
        this.dynamicJobFactoryRegistry = dynamicJobFactoryRegistry;
        this.jobProcessor = jobProcessor;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob => RunScheduledJob<TJob>(TimeSpan.Zero, parameter, token);

    /// <inheritdoc />
    public void RunInstantJob(Delegate jobDelegate) => RunScheduledJob(jobDelegate, TimeSpan.Zero);

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
    public void RunScheduledJob(Delegate jobDelegate, TimeSpan delay)
    {
        var utcNow = timeProvider.GetUtcNow();
        RunScheduledJob(jobDelegate, utcNow + delay);
    }

    /// <inheritdoc />
    public void RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate)
    {
        var definition = dynamicJobFactoryRegistry.Add(jobDelegate);
        var run = JobRun.Create(definition);
        run.Priority = JobPriority.High;
        run.RunAt = startDate;
        run.IsOneTimeJob = true;

        var jobQueue = jobQueueManager.GetOrAddQueue(run.JobDefinition.JobFullName);
        jobQueue.EnqueueForDirectExecution(run, startDate);
    }

    /// <inheritdoc />
    public void ForceRunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob => ForceRunInstantJob<TJob>(TimeSpan.Zero, parameter, token);
    
    /// <inheritdoc />
    public void ForceRunInstantJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
        where TJob : IJob
    {
        var utcNow = timeProvider.GetUtcNow();
        RunJob<TJob>(utcNow + delay, parameter, true,token);
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

            var oldJobDefinition = jobRegistry.GetJobDefinition<TJob>();
            // copy the elements from the old list to the new list
            newJobDefinition.RunWhenSuccess = [..oldJobDefinition.RunWhenSuccess];
            newJobDefinition.RunWhenFaulted = [..oldJobDefinition.RunWhenFaulted];

            token.Register(() => LogCancellationRequested(parameter));

            var run = JobRun.Create(newJobDefinition, parameter, token);
            run.Priority = JobPriority.High;
            run.RunAt = startDate;
            run.IsOneTimeJob = true;

            var jobQueue = jobQueueManager.GetOrAddQueue(run.JobDefinition.JobFullName);

            if (forceExecution)
            {
                _ = jobProcessor.ProcessJobAsync(run, token);
            }
            else
            {
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
