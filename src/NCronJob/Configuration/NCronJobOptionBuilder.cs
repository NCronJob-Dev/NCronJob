using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Text;

namespace NCronJob;

/// <summary>
/// Represents the builder for the NCronJob options.
/// </summary>
public class NCronJobOptionBuilder
{
    private protected readonly IServiceCollection Services;
    private protected readonly ConcurrencySettings Settings;
    private readonly List<JobDefinition> jobs;

    internal NCronJobOptionBuilder(
        IServiceCollection services,
        ConcurrencySettings settings,
        List<JobDefinition>? jobs = null)
    {
        Services = services;
        Settings = settings;
        this.jobs = jobs ?? [];
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
        ValidateConcurrencySetting(typeof(T));

        var builder = new JobOptionBuilder(TimeProvider.System);
        options?.Invoke(builder);
        
        Services.TryAddScoped<T>();

        var jobOptions = builder.GetJobOptions();

        foreach (var option in jobOptions)
        {
            var cron = option.CronExpression is not null
                ? GetCronExpression(option)
                : null;
            var entry = new JobDefinition(typeof(T), option.Parameter, cron, option.TimeZoneInfo)
            {
                IsStartupJob = option.IsStartupJob
            };
            jobs.Add(entry);
        }


        return new NCronJobOptionBuilder<T>(Services, Settings, jobs, builder);
    }

    /// <summary>
    /// Adds a job using an asynchronous anonymous delegate to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed.</param>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
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
        ValidateConcurrencySetting(jobDelegate.Method);

        var determinedPrecision = JobOptionBuilder.DetermineAndValidatePrecision(cronExpression, null);

        var jobOption = new JobOption
        {
            CronExpression = cronExpression,
            EnableSecondPrecision = determinedPrecision,
            TimeZoneInfo = TimeZoneInfo.Utc
        };
        var cron = GetCronExpression(jobOption);

        var jobName = GenerateJobName(jobDelegate);

        var jobPolicyMetadata = new JobExecutionAttributes(jobDelegate);
        var entry = new JobDefinition(jobType, null, cron, jobOption.TimeZoneInfo,
            JobName: jobName,
            JobPolicyMetadata: jobPolicyMetadata);
        Services.AddSingleton(entry);

        return jobName;
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

    internal void RegisterJobs()
    {
        foreach (var job in jobs)
        {
            Services.AddSingleton(job);
        }
    }
}

/// <summary>
/// Represents the builder for the NCronJob options.
/// </summary>
public sealed class NCronJobOptionBuilder<TJob> : NCronJobOptionBuilder
    where TJob : class, IJob
{
    private readonly List<JobDefinition> jobs;
    private readonly JobOptionBuilder jobOptionBuilder;

    internal NCronJobOptionBuilder(IServiceCollection services, ConcurrencySettings settings, List<JobDefinition> jobs, JobOptionBuilder jobOptionBuilder)
        : base(services, settings, jobs)
    {
        this.jobs = jobs;
        this.jobOptionBuilder = jobOptionBuilder;
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
        return new NCronJobOptionBuilder<TJobDefinition>(Services, Settings, jobs, jobOptionBuilder);
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
    /// Configures the job to run once during the application startup before any other jobs.
    /// </summary>
    /// <returns>Returns a <see cref="NCronJobOptionBuilder{TJob}"/> that allows adding parameters to the job.</returns>
    public NCronJobOptionBuilder<TJob> RunAtStartup()
    {
        jobOptionBuilder.SetRunAtStartup();

        return this;
    }
}
