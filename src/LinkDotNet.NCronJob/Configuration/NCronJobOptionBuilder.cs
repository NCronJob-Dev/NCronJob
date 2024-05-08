using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents the builder for the NCronJob options.
/// </summary>
public class NCronJobOptionBuilder
{
    private protected readonly IServiceCollection Services;
    private protected readonly ConcurrencySettings Settings;

    internal NCronJobOptionBuilder(
        IServiceCollection services,
        ConcurrencySettings settings)
    {
        Services = services;
        Settings = settings;
    }

    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="options">Configures the option, like the cron expression or parameters that get passed down.</param>
    /// <typeparam name="T">The job type. It will be registered scoped into the container.</typeparam>
    /// <exception cref="ArgumentException">Throws if the cron expression is invalid.</exception>
    /// <remarks>The cron expression is evaluated against UTC timezone.</remarks>
    /// <example>
    /// Registering a job that runs once every hour:
    /// <code>
    /// AddJob&lt;MyJob&gt;(c => c.WithCronExpression("0 * * * *").WithParameter("myParameter"));
    /// </code>
    /// </example>
    public NCronJobOptionBuilder<T> AddJob<T>(Action<JobOptionBuilder>? options = null)
        where T : class, IJob
    {
        var builder = new JobOptionBuilder();
        options?.Invoke(builder);
        var jobOptions = builder.GetJobOptions();

        var concurrencyAttribute = typeof(T).GetCustomAttribute<SupportsConcurrencyAttribute>();
        if (concurrencyAttribute != null && concurrencyAttribute.MaxDegreeOfParallelism > Settings.MaxDegreeOfParallelism)
        {
            throw new InvalidOperationException($"The MaxDegreeOfParallelism for {typeof(T).Name} " +
                                                $"({concurrencyAttribute.MaxDegreeOfParallelism}) cannot exceed " +
                                                $"the global limit ({Settings.MaxDegreeOfParallelism}).");
        }

        foreach (var option in jobOptions)
        {
            var cron = option.CronExpression is not null
                ? GetCronExpression(option)
                : null;
            var entry = new JobDefinition(typeof(T), option.Parameter, null, cron, option.TimeZoneInfo);
            Services.AddSingleton(entry);
        }

        Services.TryAddScoped<T>();

        return new NCronJobOptionBuilder<T>(Services, Settings);
    }

    private static CronExpression GetCronExpression(JobOption option)
    {
        var cf = option.EnableSecondPrecision ? CronFormat.IncludeSeconds : CronFormat.Standard;

        return CronExpression.TryParse(option.CronExpression, cf, out var cronExpression)
            ? cronExpression
            : throw new InvalidOperationException("Invalid cron expression");
    }
}

/// <summary>
/// Represents the builder for the NCronJob options.
/// </summary>
public sealed class NCronJobOptionBuilder<TJob> : NCronJobOptionBuilder
    where TJob : class, IJob
{
    internal NCronJobOptionBuilder(IServiceCollection services, ConcurrencySettings settings) : base(services, settings)
    {
    }

    /// <summary>
    /// Adds a notification handler for a given <see cref="IJob"/>.
    /// </summary>
    /// <typeparam name="TJobNotificationHandler">The handler-type that is used to handle the job.</typeparam>
    /// /// <typeparam name="TJobDefinition">The job type. It will be registered scoped into the container.</typeparam>
    /// <remarks>
    /// The given <see cref="IJobNotificationHandler{TJob}"/> instance is registered as a scoped service sharing the same scope as the job.
    /// Also, only one handler per job is allowed. If multiple handlers are registered, only the first one will be executed.
    /// <br/>This method is deprecated and will be removed.
    /// </remarks>
#pragma warning disable S1133 // Used to warn users not our internal usage
    [Obsolete("The job type can be automatically inferred. Use AddNotificationHandler<TJobNotificationHandler> instead.", error: false)]
#pragma warning restore S1133
    public NCronJobOptionBuilder<TJobDefinition> AddNotificationHandler<TJobNotificationHandler, TJobDefinition>()
        where TJobNotificationHandler : class, IJobNotificationHandler<TJobDefinition>
        where TJobDefinition : class, IJob
    {
        Services.TryAddScoped<IJobNotificationHandler<TJobDefinition>, TJobNotificationHandler>();
        return new NCronJobOptionBuilder<TJobDefinition>(Services, Settings);
    }

    /// <summary>
    /// Adds a notification handler for a given <see cref="IJob"/>.
    /// </summary>
    /// <typeparam name="TJobNotificationHandler">The handler-type that is used to handle the job.</typeparam>
    /// <remarks>
    /// The given <see cref="IJobNotificationHandler{TJob}"/> instance is registered as a scoped service sharing the same scope as the job.
    /// Also, only one handler per job is allowed. If multiple handlers are registered, only the first one will be executed.
    /// </remarks>
    public NCronJobOptionBuilder<TJob> AddNotificationHandler<TJobNotificationHandler>()
        where TJobNotificationHandler : class, IJobNotificationHandler<TJob>
    {
        Services.TryAddScoped<IJobNotificationHandler<TJob>, TJobNotificationHandler>();
        return this;
    }

    /// <summary>
    /// Adds a job that runs after the given job has finished.
    /// </summary>
    /// <param name="success">Configure a job that runs after the principal job has finished successfully.</param>
    /// <param name="faulted">Configure a job that runs after the principal job has faulted. Faulted means that the parent job did throw an exception.</param>
    /// <returns>The builder to add more jobs.</returns>
    public NCronJobOptionBuilder<TJob> When(Action<DependencyBuilder<TJob>>? success = null, Action<DependencyBuilder<TJob>>? faulted = null)
    {
        if (success is not null)
        {
            var dependencyBuilder = new DependencyBuilder<TJob>(true);
            success(dependencyBuilder);
            dependencyBuilder.GetDependentJobOption().ForEach(s =>
            {
                Services.AddSingleton(s);
                Services.TryAddSingleton(s.DependentJobType);
            });
        }

        if (faulted is not null)
        {
            var dependencyBuilder = new DependencyBuilder<TJob>(false);
            faulted(dependencyBuilder);
            dependencyBuilder.GetDependentJobOption().ForEach(s =>
            {
                Services.AddSingleton(s);
                Services.TryAddSingleton(s.DependentJobType);
            });
        }

        return this;
    }
}

/// <summary>
/// Represents the builder for the dependent jobs.
/// </summary>
public sealed class DependencyBuilder<TPrincipalJob>
    where TPrincipalJob : IJob
{
    private readonly bool runOnSuccess;
    private readonly List<DependentJobOption> dependentJobOptions = [];

    internal DependencyBuilder(bool runOnSuccess) => this.runOnSuccess = runOnSuccess;

    /// <summary>
    /// Adds a job that runs after the principal job has finished with a given <paramref name="parameter"/>.
    /// </summary>
    public DependencyBuilder<TPrincipalJob> RunJob<TJob>(object? parameter = null)
        where TJob : IJob
    {
        dependentJobOptions.Add(new DependentJobOption(typeof(TPrincipalJob), typeof(TJob), runOnSuccess, parameter));
        return this;
    }

    internal List<DependentJobOption> GetDependentJobOption() => dependentJobOptions;
}
