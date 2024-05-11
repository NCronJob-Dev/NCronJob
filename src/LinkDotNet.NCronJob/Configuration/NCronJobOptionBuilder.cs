using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Reflection;
using System.Text;

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

        foreach (var option in jobOptions.Where(c => !string.IsNullOrEmpty(c.CronExpression)))
        {
            var cron = GetCronExpression(option);
            var retryPolicy = typeof(T).GetCustomAttribute<RetryPolicyAttribute>();
            var entry = new JobDefinition(typeof(T), option.Parameter, cron, option.TimeZoneInfo, RetryPolicy: retryPolicy);
            Services.AddSingleton(entry);
        }

        Services.TryAddScoped<T>();

        return new NCronJobOptionBuilder<T>(Services, Settings);
    }

    /// <summary>
    /// Adds a job using an asynchronous anonymous delegate to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed.</param>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <returns></returns>
    public NCronJobOptionBuilder AddJob(Delegate jobDelegate, string cronExpression)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);

        var jobName = AddJobInternal(typeof(DynamicJobFactory), jobDelegate, cronExpression);
        Services.AddKeyedSingleton(typeof(DynamicJobFactory), jobName, (sp, _) =>
            new DynamicJobFactory(sp, jobDelegate));
        return this;
    }

    /// <summary>
    /// Registers a job in the service collection with specified delegate and cron expression.
    /// This method configures job options, validates the cron expression, and creates a registry entry for the job.
    /// </summary>
    /// <param name="jobType">The type of the job to be registered.</param>
    /// <param name="jobDelegate">The delegate that represents the job's execution logic. This can be either a synchronous or asynchronous delegate.</param>
    /// <param name="cronExpression">The cron expression that specifies the schedule on which the job should be executed.</param>
    /// <returns>The name generated for the job, which is used as the key for scoped service registration.
    /// This name is derived from the job delegate and is used to uniquely identify the job in the service collection.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided <paramref name="cronExpression"/> is null or empty.</exception>
    private string AddJobInternal(Type jobType, Delegate jobDelegate, string cronExpression)
    {
        ArgumentException.ThrowIfNullOrEmpty(cronExpression);

        var determinedPrecision = JobOptionBuilder.DetermineAndValidatePrecision(cronExpression, null);

        var jobOption = new JobOption
        {
            CronExpression = cronExpression,
            EnableSecondPrecision = determinedPrecision,
            TimeZoneInfo = TimeZoneInfo.Utc
        };
        var cron = GetCronExpression(jobOption);

        var jobName = GenerateJobName(jobDelegate);

        var methodInfo = jobDelegate.Method;
        var retryPolicy = methodInfo.GetCustomAttribute<RetryPolicyAttribute>();
        var entry = new JobDefinition(jobType, null, cron, jobOption.TimeZoneInfo, JobName: jobName, RetryPolicy: retryPolicy);
        Services.AddSingleton(entry);

        return jobName;
    }

    internal static CronExpression GetCronExpression(JobOption option)
    {
        var cf = option.EnableSecondPrecision ? CronFormat.IncludeSeconds : CronFormat.Standard;

        return CronExpression.TryParse(option.CronExpression, cf, out var cronExpression)
            ? cronExpression
            : throw new InvalidOperationException("Invalid cron expression");
    }

    /// <summary>
    /// Construct a consistent job name from the delegate's method signature and a hash of its target
    /// </summary>
    /// <param name="jobDelegate">The delegate to generate a name for</param>
    /// <returns></returns>
    internal static string GenerateJobName(Delegate jobDelegate)
    {
        var methodInfo = jobDelegate.GetMethodInfo();
        var jobNameBuilder = new StringBuilder(methodInfo.DeclaringType!.FullName);
        jobNameBuilder.Append(methodInfo.Name);

        foreach (var param in methodInfo.GetParameters())
        {
            jobNameBuilder.Append(param.ParameterType.Name);
        }
        var jobHash = jobNameBuilder.ToString().GenerateConsistentShortHash();
        return $"AnonymousJob_{jobHash}";
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
}
