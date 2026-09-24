using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace NCronJob;

/// <summary>
/// Represents the builder for the NCronJob options.
/// </summary>
public class NCronJobOptionBuilder : IJobStage, IRuntimeJobBuilder
{
    private readonly IServiceCollection services;
    private readonly ConcurrencySettings settings;
    private readonly JobDefinitionCollector jobDefinitionCollector;

    internal NCronJobOptionBuilder(
        IServiceCollection services,
        ConcurrencySettings settings,
        JobDefinitionCollector jobDefinitionCollector)
    {
        this.services = services;
        this.settings = settings;
        this.jobDefinitionCollector = jobDefinitionCollector;
    }

    /// <summary>
    /// Configures the scheduler-wide maximum number of jobs that may execute concurrently.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent job executions.</param>
    /// <returns>The same builder so additional scheduler options and jobs can be configured.</returns>
    public NCronJobOptionBuilder WithMaxDegreeOfParallelism(int maxDegreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDegreeOfParallelism);
        settings.MaxDegreeOfParallelism = maxDegreeOfParallelism;
        return this;
    }

    /// <summary>
    /// Configures how long scheduled jobs may remain queued after their intended run time before expiring.
    /// </summary>
    /// <param name="expiry">A positive duration, or <see cref="Timeout.InfiniteTimeSpan"/> to disable expiry.</param>
    /// <returns>The same builder so additional scheduler options and jobs can be configured.</returns>
    public NCronJobOptionBuilder WithDefaultJobRunExpiry(TimeSpan expiry)
    {
        JobOption.ValidateTimeoutLikeValue(expiry, nameof(expiry));
        settings.DefaultJobRunExpiry = expiry;
        return this;
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
        var jobDefinitions = AddJobInternal(typeof(T), options);

        jobDefinitionCollector.Add(jobDefinitions);

        return new StartupStage<T>(services, jobDefinitions, settings, jobDefinitionCollector);
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
        ArgumentNullException.ThrowIfNull(jobType);

        var jobDefinitions = AddJobInternal(jobType, options);

        jobDefinitionCollector.Add(jobDefinitions);

        return new StartupStage<IJob>(services, jobDefinitions, settings, jobDefinitionCollector);
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
            TimeZoneInfo = timeZoneInfo
        };

        var jobDefinition = JobDefinition.CreateUntyped(jobName, jobDelegate);
        jobDefinition.UpdateWith(jobOption);

        jobDefinitionCollector.Add(jobDefinition);

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
        services.AddSingleton<IExceptionHandler, TExceptionHandler>();
        return this;
    }

    void IRuntimeJobBuilder.AddJob(Type jobType, Action<JobOptionBuilder>? options) => AddJob(jobType, options);

    void IRuntimeJobBuilder.AddJob(Delegate jobDelegate, string cronExpression, TimeZoneInfo? timeZoneInfo, string? jobName) =>
        AddJob(jobDelegate, cronExpression, timeZoneInfo, jobName);

    internal void ValidateConcurrencySettings(IReadOnlyCollection<JobDefinition> existingJobDefinitions)
    {
        foreach (var jobDefinition in existingJobDefinitions.Concat(jobDefinitionCollector.Entries.Keys))
        {
            ValidateConcurrencySetting(jobDefinition.Name, jobDefinition.ConcurrencyPolicy);
        }

        var dependentJobDefinitions = jobDefinitionCollector.Entries.Values
            .SelectMany(entries => entries)
            .SelectMany(entry => entry.RunWhenSuccess.Concat(entry.RunWhenFaulted));

        foreach (var jobDefinition in dependentJobDefinitions)
        {
            ValidateConcurrencySetting(jobDefinition.Name, jobDefinition.ConcurrencyPolicy);
        }
    }

    private void ValidateConcurrencySetting(
        string jobName,
        SupportsConcurrencyAttribute? concurrencyAttribute)
    {
        if (concurrencyAttribute is not null && concurrencyAttribute.MaxDegreeOfParallelism > settings.MaxDegreeOfParallelism)
        {
            throw new InvalidOperationException(
                $"The MaxDegreeOfParallelism for {jobName} ({concurrencyAttribute.MaxDegreeOfParallelism}) cannot exceed the global limit ({settings.MaxDegreeOfParallelism}).");
        }
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
        if (concurrencyAttribute is not null && concurrencyAttribute.MaxDegreeOfParallelism > settings.MaxDegreeOfParallelism)
        {
            var name = jobIdentifier is Type type ? type.Name : ((MethodInfo)jobIdentifier).Name;
            throw new InvalidOperationException(
                $"The MaxDegreeOfParallelism for {name} ({concurrencyAttribute.MaxDegreeOfParallelism}) cannot exceed the global limit ({settings.MaxDegreeOfParallelism}).");
        }
    }

    private List<JobDefinition> AddJobInternal(
        Type jobType,
        Action<JobOptionBuilder>? options)
    {
        ValidateConcurrencySetting(jobType);

        List<JobDefinition> jobDefinitions = [];

        var builder = new JobOptionBuilder();
        options?.Invoke(builder);

        services.TryAddScoped(jobType);

        var jobOptions = builder.GetJobOptions();

        foreach (var option in jobOptions)
        {
            var entry = JobDefinition.CreateTyped(option.Name, jobType, option.Parameter);
            entry.UpdateWith(option);

            jobDefinitions.Add(entry);
        }

        return jobDefinitions;
    }
}

