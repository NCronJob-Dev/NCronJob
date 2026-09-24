using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCronJob;

internal abstract class JobStage<TJob> : INotificationStage<TJob> where TJob : class, IJob
{
    protected JobStage(
        IServiceCollection services,
        IReadOnlyCollection<JobDefinition> jobDefinitions,
        ConcurrencySettings settings,
        JobDefinitionCollector jobDefinitionCollector)
    {
        Services = services;
        JobDefinitions = jobDefinitions;
        Settings = settings;
        JobDefinitionCollector = jobDefinitionCollector;
    }

    protected IServiceCollection Services { get; }

    protected IReadOnlyCollection<JobDefinition> JobDefinitions { get; }

    protected ConcurrencySettings Settings { get; }

    protected JobDefinitionCollector JobDefinitionCollector { get; }

    /// <inheritdoc />
    public INotificationStage<TJob> AddNotificationHandler<TJobNotificationHandler>() where TJobNotificationHandler : class, IJobNotificationHandler<TJob>
    {
        Services.TryAddScoped<IJobNotificationHandler<TJob>, TJobNotificationHandler>();
        return AsNotificationStage();
    }

    /// <inheritdoc />
    public INotificationStage<TJob> AddConditionHandler<TJobConditionHandler>() where TJobConditionHandler : class, IJobConditionHandler<TJob>
    {
        Services.TryAddScoped<IJobConditionHandler<TJob>, TJobConditionHandler>();
        return AsNotificationStage();
    }

    /// <inheritdoc />
    public INotificationStage<TJob> ExecuteWhen(Action<DependencyBuilder>? success = null, Action<DependencyBuilder>? faulted = null)
    {
        ExecuteWhenHelper.AddRegistration(JobDefinitionCollector, JobDefinitions, success, faulted);

        return this;
    }

    /// <inheritdoc />
    public IStartupStage<TNewJob> AddJob<TNewJob>(Action<JobOptionBuilder>? options = null) where TNewJob : class, IJob
        => new NCronJobOptionBuilder(Services, Settings, JobDefinitionCollector).AddJob<TNewJob>(options);

    /// <inheritdoc />
    public IStartupStage<IJob> AddJob(Type jobType, Action<JobOptionBuilder>? options = null)
        => new NCronJobOptionBuilder(Services, Settings, JobDefinitionCollector).AddJob(jobType, options);

    protected abstract INotificationStage<TJob> AsNotificationStage();
}
