using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCronJob;

/// <summary>
/// Represents a stage in the job lifecycle where notifications are handled for the job.
/// </summary>
/// <typeparam name="TJob">The type of the job for which notifications are handled.</typeparam>
internal sealed class NotificationStage<TJob> : INotificationStage<TJob> where TJob : class, IJob
{
    internal NotificationStage(
        IServiceCollection services,
        IReadOnlyCollection<JobDefinition> jobDefinitions,
        ConcurrencySettings settings,
        PendingJobDefinitions pendingJobDefinitions)
    {
        Services = services;
        JobDefinitions = jobDefinitions;
        Settings = settings;
        PendingJobDefinitions = pendingJobDefinitions;
    }

    private IServiceCollection Services { get; }

    private IReadOnlyCollection<JobDefinition> JobDefinitions { get; }

    private ConcurrencySettings Settings { get; }

    private PendingJobDefinitions PendingJobDefinitions { get; }

    /// <inheritdoc />
    public INotificationStage<TJob> AddNotificationHandler<TJobNotificationHandler>() where TJobNotificationHandler : class, IJobNotificationHandler<TJob>
    {
        Services.TryAddScoped<IJobNotificationHandler<TJob>, TJobNotificationHandler>();
        return this;
    }

    /// <inheritdoc />
    public INotificationStage<TJob> AddConditionHandler<TJobConditionHandler>() where TJobConditionHandler : class, IJobConditionHandler<TJob>
    {
        Services.TryAddScoped<IJobConditionHandler<TJob>, TJobConditionHandler>();
        return this;
    }

    /// <inheritdoc />
    public INotificationStage<TJob> ExecuteWhen(Action<DependencyBuilder>? success = null, Action<DependencyBuilder>? faulted = null)
    {
        DependentJobRegistration.Register(PendingJobDefinitions, JobDefinitions, success, faulted);

        return this;
    }

    /// <inheritdoc />
    public INotificationStage<TNewJob> AddJob<TNewJob>(Action<JobOptionBuilder>? options = null) where TNewJob : class, IJob
        => new NCronJobOptionBuilder(Services, Settings, PendingJobDefinitions).AddJob<TNewJob>(options);

    /// <inheritdoc />
    public INotificationStage<IJob> AddJob(Type jobType, Action<JobOptionBuilder>? options = null)
        => new NCronJobOptionBuilder(Services, Settings, PendingJobDefinitions).AddJob(jobType, options);
}
