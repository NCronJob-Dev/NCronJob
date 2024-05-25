using System.Collections.Immutable;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly ImmutableArray<JobDefinition> cronJobs;
    private readonly ImmutableArray<JobDefinition> oneTimeJobs;
    private readonly ImmutableArray<Type> allJobTypes;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJobTypes = [..jobDefinitions.Select(j => j.Type)];
        cronJobs = [..jobDefinitions.Where(c => c.CronExpression is not null)];
        oneTimeJobs = [..jobDefinitions.Where(c => c.IsStartupJob)];
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;
    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;

    public bool IsJobRegistered<T>() => allJobTypes.Any(j => j == typeof(T));
}

