using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace NCronJob;

/// <summary>
/// Represents the builder for the NCronJob options.
/// </summary>
public class NCronJobOptionBuilder : IJobStage
{
    private protected readonly IServiceCollection Services;
    private protected readonly ConcurrencySettings Settings;
    private readonly JobRegistry jobRegistry;

    internal NCronJobOptionBuilder(
        IServiceCollection services,
        ConcurrencySettings settings,
        JobRegistry jobRegistry)
    {
        Services = services;
        Settings = settings;
        this.jobRegistry = jobRegistry;
    }

    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    /// <typeparam name="T">The job type. It will be registered scoped into the container.</typeparam>
    /// <exception cref="ArgumentException">Throws if the cron expression is invalid.</exception>
    /// <remarks>The cron expression is evaluated against the TimeZoneInfo of the <see cref="JobOptionBuilder"/>.</remarks>
    /// <example>
    /// Registering a job that runs once every hour:
    /// <code>
    /// AddJob&lt;MyJob&gt;(c => c.WithCronExpression("0 * * * *").WithParameter("myParameter"));
    /// </code>
    /// </example>
    public IStartupStage<T> AddJob<T>(Action<JobOptionBuilder>? options = null)
        where T : class, IJob
    {
        var builder = AddJobInternal(typeof(T), options);
        return new StartupStage<T>(Services, Settings, jobRegistry, builder);
    }

    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobType">The job type. It will be registered scoped into the container.</param>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    /// <exception cref="ArgumentException">Throws if the cron expression is invalid.</exception>
    /// <remarks>The cron expression is evaluated against the TimeZoneInfo of the <see cref="JobOptionBuilder"/>.</remarks>
    /// <example>
    /// Registering a job that runs once every hour:
    /// <code>
    /// AddJob&lt;MyJob&gt;(c => c.WithCronExpression("0 * * * *").WithParameter("myParameter"));
    /// </code>
    /// </example>
    public IStartupStage<IJob> AddJob(Type jobType, Action<JobOptionBuilder>? options = null)
    {
        var builder = AddJobInternal(jobType, options);
        return new StartupStage<IJob>(Services, Settings, jobRegistry, builder);
    }

    /// <summary>
    /// Adds a job using an asynchronous anonymous delegate to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed.</param>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="timeZoneInfo">The time zone information that the cron expression should be evaluated against.
    /// If not set the default time zone is UTC.
    /// </param>
    /// <param name="jobName">Sets the job name that can be used to identify and manipulate the job later on.</param>
    public NCronJobOptionBuilder AddJob(
        Delegate jobDelegate,
        string cronExpression,
        TimeZoneInfo? timeZoneInfo = null,
        string? jobName = null)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);
        ArgumentException.ThrowIfNullOrEmpty(cronExpression);

        ValidateConcurrencySetting(jobDelegate.Method);

        var jobOption = new JobOption
        {
            CronExpression = cronExpression,
            TimeZoneInfo = timeZoneInfo ?? TimeZoneInfo.Utc
        };

        jobRegistry.AddDynamicJob(jobDelegate, jobName, jobOption);

        return this;
    }

    /// <summary>
    /// Registers the <see cref="IExceptionHandler"/> implementation to the service collection.
    /// </summary>
    /// <remarks>The order of the exception handlers is important.
    /// The first handler registered will be the first one to be called.
    /// If the handler returns <c>true</c> no other exception handlers will be called.
    /// </remarks>
    public NCronJobOptionBuilder AddExceptionHandler<TExceptionHandler>() where TExceptionHandler : class, IExceptionHandler
    {
        Services.AddSingleton<IExceptionHandler, TExceptionHandler>();
        return this;
    }

    private void ValidateConcurrencySetting(object jobIdentifier)
    {
        var cachedJobAttributes = jobIdentifier switch
        {
            Type type => JobAttributeCache.GetJobExecutionAttributes(type),
            MethodInfo methodInfo => JobAttributeCache.GetJobExecutionAttributes(methodInfo),
            _ => throw new ArgumentException("Invalid job identifier type")
        };

        var concurrencyAttribute = cachedJobAttributes.ConcurrencyPolicy;
        if (concurrencyAttribute != null && concurrencyAttribute.MaxDegreeOfParallelism > Settings.MaxDegreeOfParallelism)
        {
            var name = jobIdentifier is Type type ? type.Name : ((MethodInfo)jobIdentifier).Name;
            throw new InvalidOperationException(
                $"The MaxDegreeOfParallelism for {name} ({concurrencyAttribute.MaxDegreeOfParallelism}) cannot exceed the global limit ({Settings.MaxDegreeOfParallelism}).");
        }
    }

    internal static CronExpression GetCronExpression(string expression)
    {
        var precisionRequired = DetermineAndValidatePrecision(expression);

        var cf = precisionRequired ? CronFormat.IncludeSeconds : CronFormat.Standard;

        return CronExpression.TryParse(expression, cf, out var cronExpression)
            ? cronExpression
            : throw new InvalidOperationException("Invalid cron expression");
    }

    private JobOptionBuilder AddJobInternal(Type jobType, Action<JobOptionBuilder>? options)
    {
        ValidateConcurrencySetting(jobType);

        var builder = new JobOptionBuilder();
        options?.Invoke(builder);

        Services.TryAddScoped(jobType);

        var jobOptions = builder.GetJobOptions();

        foreach (var option in jobOptions)
        {
            var cron = option.CronExpression is not null
                ? GetCronExpression(option.CronExpression)
                : null;
            var entry = new JobDefinition(jobType, option.Parameter, cron, option.TimeZoneInfo)
            {
                IsStartupJob = option.IsStartupJob,
                CustomName = option.Name,
                UserDefinedCronExpression = option.CronExpression
            };
            jobRegistry.Add(entry);
        }

        return builder;
    }

    private static bool DetermineAndValidatePrecision(string cronExpression)
    {
        var parts = cronExpression.Split(' ');
        var precisionRequired = parts.Length == 6;

        var expectedLength = precisionRequired ? 6 : 5;
        if (parts.Length != expectedLength)
        {
            var precisionText = precisionRequired ? "second precision" : "minute precision";
            throw new ArgumentException($"Invalid cron expression format for {precisionText}.", nameof(cronExpression));
        }

        return precisionRequired;
    }
}

/// <summary>
/// Represents a stage in the job lifecycle where the job is set to run at startup.
/// </summary>
/// <typeparam name="TJob">The type of the job to be run at startup.</typeparam>
internal class StartupStage<TJob> : IStartupStage<TJob> where TJob : class, IJob
{
    private readonly IServiceCollection services;
    private readonly ConcurrencySettings settings;
    private readonly JobRegistry jobRegistry;
    private readonly JobOptionBuilder jobOptionBuilder;

    internal StartupStage(
        IServiceCollection services,
        ConcurrencySettings settings,
        JobRegistry jobRegistry,
        JobOptionBuilder jobOptionBuilder)
    {
        this.services = services;
        this.settings = settings;
        this.jobRegistry = jobRegistry;
        this.jobOptionBuilder = jobOptionBuilder;
    }

    /// <inheritdoc />
    public INotificationStage<TJob> RunAtStartup()
    {
        jobOptionBuilder.SetRunAtStartup();

        jobRegistry.UpdateJobDefinitionsToRunAtStartup<TJob>();

        return new NotificationStage<TJob>(services, settings, jobRegistry);
    }

    /// <inheritdoc />
    public INotificationStage<TJob> AddNotificationHandler<TJobNotificationHandler>() where TJobNotificationHandler : class, IJobNotificationHandler<TJob>
    {
        services.TryAddScoped<IJobNotificationHandler<TJob>, TJobNotificationHandler>();
        return new NotificationStage<TJob>(services, settings, jobRegistry);
    }

    /// <inheritdoc />
    public INotificationStage<TJob> ExecuteWhen(Action<DependencyBuilder<TJob>>? success = null, Action<DependencyBuilder<TJob>>? faulted = null)
    {
        ExecuteWhenHelper.AddRegistration(jobRegistry, success, faulted);

        return this;
    }

    /// <inheritdoc />
    public IStartupStage<TNewJob> AddJob<TNewJob>(Action<JobOptionBuilder>? options = null) where TNewJob : class, IJob
        => new NCronJobOptionBuilder(services, settings, jobRegistry).AddJob<TNewJob>(options);

    /// <inheritdoc />
    public IStartupStage<IJob> AddJob(Type jobType, Action<JobOptionBuilder>? options = null)
        => new NCronJobOptionBuilder(services, settings, jobRegistry).AddJob(jobType, options);
}

/// <summary>
/// Represents a stage in the job lifecycle where notifications are handled for the job.
/// </summary>
/// <typeparam name="TJob">The type of the job for which notifications are handled.</typeparam>
internal class NotificationStage<TJob> : INotificationStage<TJob> where TJob : class, IJob
{
    private readonly IServiceCollection services;
    private readonly ConcurrencySettings settings;
    private readonly JobRegistry jobRegistry;

    internal NotificationStage(
        IServiceCollection services,
        ConcurrencySettings settings,
        JobRegistry jobRegistry)
    {
        this.services = services;
        this.settings = settings;
        this.jobRegistry = jobRegistry;
    }

    /// <inheritdoc />
    public INotificationStage<TJob> AddNotificationHandler<TJobNotificationHandler>() where TJobNotificationHandler : class
        , IJobNotificationHandler<TJob>
    {
        services.TryAddScoped<IJobNotificationHandler<TJob>, TJobNotificationHandler>();
        return this;
    }

    /// <inheritdoc />
    public INotificationStage<TJob> ExecuteWhen(Action<DependencyBuilder<TJob>>? success = null,
        Action<DependencyBuilder<TJob>>? faulted = null)
    {
        ExecuteWhenHelper.AddRegistration(jobRegistry, success, faulted);

        return this;
    }

    /// <inheritdoc />
    public IStartupStage<TNewJob> AddJob<TNewJob>(Action<JobOptionBuilder>? options = null) where TNewJob : class, IJob =>
        new NCronJobOptionBuilder(services, settings, jobRegistry).AddJob<TNewJob>(options);

    /// <inheritdoc />
    public IStartupStage<IJob> AddJob(Type jobType, Action<JobOptionBuilder>? options = null)
        => new NCronJobOptionBuilder(services, settings, jobRegistry).AddJob(jobType, options);
}

/// <summary>
/// Defines the contract for a stage in the job lifecycle.
/// </summary>
public interface IJobStage
{
    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    /// <typeparam name="TJob">The job type. It will be registered scoped into the container.</typeparam>
    /// <exception cref="ArgumentException">Throws if the cron expression is invalid.</exception>
    /// <remarks>The cron expression is evaluated against the TimeZoneInfo of the <see cref="JobOptionBuilder"/>.</remarks>
    /// <example>
    /// Registering a job that runs once every hour:
    /// <code>
    /// AddJob&lt;MyJob&gt;(c => c.WithCronExpression("0 * * * *").WithParameter("myParameter"));
    /// </code>
    /// </example>
    IStartupStage<TJob> AddJob<TJob>(Action<JobOptionBuilder>? options = null) where TJob : class, IJob;
    
    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    IStartupStage<IJob> AddJob(Type jobType, Action<JobOptionBuilder>? options = null);
}

/// <summary>
/// Defines the contract for a stage in the job lifecycle where the job is set to run at startup.
/// </summary>
/// <typeparam name="TJob">The type of the job to be run at startup.</typeparam>
public interface IStartupStage<TJob> : INotificationStage<TJob>
    where TJob : class, IJob
{
    /// <summary>
    /// Configures the job to run once during the application startup before any other jobs.
    /// </summary>
    /// <returns>Returns a <see cref="INotificationStage{TJob}"/> that allows adding notifications of another job.</returns>
    INotificationStage<TJob> RunAtStartup();
}

/// <summary>
/// Defines the contract for a stage in the job lifecycle where notifications are handled for the job.
/// </summary>
/// <typeparam name="TJob">The type of the job for which notifications are handled.</typeparam>
public interface INotificationStage<TJob> : IJobStage
    where TJob : class, IJob
{
    /// <summary>
    /// Adds a notification handler for a given <see cref="IJob"/>.
    /// </summary>
    /// <typeparam name="TJobNotificationHandler">The handler-type that is used to handle the job.</typeparam>
    /// <remarks>
    /// The given <see cref="IJobNotificationHandler{TJob}"/> instance is registered as a scoped service sharing the same scope as the job.
    /// Also, only one handler per job is allowed. If multiple handlers are registered, only the first one will be executed.
    /// </remarks>
    INotificationStage<TJob> AddNotificationHandler<TJobNotificationHandler>() where TJobNotificationHandler : class, IJobNotificationHandler<TJob>;

    /// <summary>
    /// Adds a job that runs after the given job has finished.
    /// </summary>
    /// <param name="success">Configure a job that runs after the principal job has finished successfully.</param>
    /// <param name="faulted">Configure a job that runs after the principal job has faulted. Faulted means that the parent job did throw an exception.</param>
    /// <returns>The builder to add more jobs.</returns>
    INotificationStage<TJob> ExecuteWhen(
        Action<DependencyBuilder<TJob>>? success = null,
        Action<DependencyBuilder<TJob>>? faulted = null);
}

internal static class ExecuteWhenHelper
{
    public static void AddRegistration<TJob>(
        JobRegistry jobRegistry,
        Action<DependencyBuilder<TJob>>? success,
        Action<DependencyBuilder<TJob>>? faulted)
        where TJob : IJob
    {
        if (success is not null)
        {
            var dependencyBuilder = new DependencyBuilder<TJob>(jobRegistry);
            success(dependencyBuilder);
            var runWhenSuccess = dependencyBuilder.GetDependentJobOption();
            var entry = new DependentJobRegistryEntry
            {
                PrincipalType = typeof(TJob),
                RunWhenSuccess = runWhenSuccess,

            };
            jobRegistry.RegisterJobDependency(entry);
        }

        if (faulted is not null)
        {
            var dependencyBuilder = new DependencyBuilder<TJob>(jobRegistry);
            faulted(dependencyBuilder);
            var runWhenFaulted = dependencyBuilder.GetDependentJobOption();
            var entry = new DependentJobRegistryEntry
            {
                PrincipalType = typeof(TJob),
                RunWhenFaulted = runWhenFaulted,

            };
            jobRegistry.RegisterJobDependency(entry);
        }
    }
}
