using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NCronJob;

internal class JobRegistrator
{
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
