using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NCronJob;

/// <summary>
/// Defines the contract to register jobs in the service collection.
/// </summary>
public interface IJobRegistrator
{
    /// <summary>
    /// Adds a typed job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobType">The job type. It will be registered scoped into the container.</param>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    IDependencyAndJobRegistrator AddJob(Type jobType, Action<JobOptionBuilder>? options = null);

    /// <summary>
    /// Adds an untypedjob to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed.</param>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    IDependencyAndJobRegistrator AddJob(Delegate jobDelegate, Action<JobOptionBuilder>? options = null);
}

/// <summary>
/// Extensions for IJobRegistrator.
/// </summary>
public static class IJobRegistratorExtensions
{
    /// <summary>
    /// Adds a typed job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <typeparam name="T">The job type. It will be registered scoped into the container.</typeparam>
    /// <param name="jobRegistrator"> The job registrator instance.</param>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    public static IDependencyAndJobRegistrator AddJob<T>(
        this IJobRegistrator jobRegistrator,
        Action<JobOptionBuilder>? options = null)
        where T : class, IJob
    {
        ArgumentNullException.ThrowIfNull(jobRegistrator);

        return jobRegistrator.AddJob(typeof(T), options);
    }
}

/// <summary>
/// Defines the contract to register jobs and dependencies in the service collection.
/// </summary>
public interface IDependencyAndJobRegistrator : IJobRegistrator
{
    /// <summary>
    /// Adds a job that runs after the given job has finished.
    /// </summary>
    /// <param name="success">Configure a job that runs after the principal job has finished successfully.</param>
    /// <param name="faulted">Configure a job that runs after the principal job has faulted. Faulted means that the parent job did throw an exception.</param>
    /// <returns>The builder to register more jobs.</returns>
    IDependencyAndJobRegistrator ExecuteWhen(
        Action<DependencyBuilder>? success = null,
        Action<DependencyBuilder>? faulted = null);
}

internal class JobRegistrator : IDependencyAndJobRegistrator
{
    private readonly IServiceCollection services;
    private readonly ConcurrencySettings settings;
    private readonly JobDefinitionCollector jobDefinitionCollector;
    private readonly IReadOnlyCollection<JobDefinition> parentJobDefinitions;

    public JobRegistrator(
        IServiceCollection services,
        ConcurrencySettings settings,
        JobDefinitionCollector jobDefinitionCollector,
        IReadOnlyCollection<JobDefinition> parentJobDefinitions)
    {
        this.services = services;
        this.settings = settings;
        this.jobDefinitionCollector = jobDefinitionCollector;
        this.parentJobDefinitions = parentJobDefinitions;
    }

    public IDependencyAndJobRegistrator AddJob(Type jobType, Action<JobOptionBuilder>? options = null)
    {
        ArgumentNullException.ThrowIfNull(jobType);

        var jobDefinitions = AddTypedJobInternal(services, settings, jobType, options);

        jobDefinitionCollector.Add(jobDefinitions);

        return new JobRegistrator(services, settings, jobDefinitionCollector, jobDefinitions);
    }

    public IDependencyAndJobRegistrator AddJob(Delegate jobDelegate, Action<JobOptionBuilder>? options = null)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);

        var jobDefinitions = AddUntypedJobInternal(settings, jobDelegate, options);

        jobDefinitionCollector.Add(jobDefinitions);

        return new JobRegistrator(services, settings, jobDefinitionCollector, jobDefinitions);
    }

    public IDependencyAndJobRegistrator ExecuteWhen(
        Action<DependencyBuilder>? success = null,
        Action<DependencyBuilder>? faulted = null)
    {
        ExecuteWhenHelper.AddRegistration(
            jobDefinitionCollector,
            parentJobDefinitions,
            success,
            faulted);

        return this;
    }

    internal static List<JobDefinition> AddTypedJobInternal(
        IServiceCollection services,
        ConcurrencySettings settings,
        Type jobType,
        Action<JobOptionBuilder>? options)
    {
        ValidateConcurrencySetting(settings, jobType);
        services.TryAddScoped(jobType);

        return AddJobInternal(
            (o) => JobDefinition.CreateTyped(o.Name, jobType, o.Parameter),
            options);
    }

    internal static List<JobDefinition> AddUntypedJobInternal(
        ConcurrencySettings settings,
        Delegate jobDelegate,
        Action<JobOptionBuilder>? options)
    {
        ValidateConcurrencySetting(settings, jobDelegate.Method);

        return AddJobInternal(
            (o) => JobDefinition.CreateUntyped(o.Name, jobDelegate),
            options);
    }

    private static List<JobDefinition> AddJobInternal(
        Func<JobOption, JobDefinition> definitionBuilder,
        Action<JobOptionBuilder>? options)
    {
        var jobDefinitions = new List<JobDefinition>();

        var jobOptions = JobOptionBuilder.Evaluate(options);

        foreach (var option in jobOptions)
        {
            var entry = definitionBuilder(option);
            entry.UpdateWith(option);

            jobDefinitions.Add(entry);
        }

        return jobDefinitions;
    }

    private static void ValidateConcurrencySetting(
        ConcurrencySettings settings,
        object jobIdentifier)
    {
        var cachedJobAttributes = jobIdentifier switch
        {
            Type type => JobAttributeCache.GetJobExecutionAttributes(type),
            MethodInfo methodInfo => JobAttributeCache.GetJobExecutionAttributes(methodInfo),
            _ => throw new ArgumentException("Invalid job identifier type")
        };

        var concurrencyAttribute = cachedJobAttributes.ConcurrencyPolicy;
        if (concurrencyAttribute != null && concurrencyAttribute.MaxDegreeOfParallelism > settings.MaxDegreeOfParallelism)
        {
            var name = jobIdentifier is Type type ? type.Name : ((MethodInfo)jobIdentifier).Name;
            throw new InvalidOperationException(
                $"The MaxDegreeOfParallelism for {name} ({concurrencyAttribute.MaxDegreeOfParallelism}) cannot exceed the global limit ({settings.MaxDegreeOfParallelism}).");
        }
    }
}
