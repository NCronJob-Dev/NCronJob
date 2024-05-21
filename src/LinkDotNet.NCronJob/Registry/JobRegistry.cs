using System.Collections.Immutable;

namespace LinkDotNet.NCronJob;

internal sealed class JobRegistry
{
    private readonly ImmutableArray<JobDefinition> cronJobs;
    private readonly ImmutableArray<Type> allJobTypes;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJobTypes = [..jobDefinitions.Select(j => j.Type)];
        cronJobs = [.. jobDefinitions.Where(c => c.CronExpression is not null)];
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;

    public bool IsJobRegistered<T>() => allJobTypes.Any(j => j == typeof(T));
}

