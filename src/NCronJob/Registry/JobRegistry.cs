using System.Collections.Immutable;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly ImmutableArray<JobDefinition> cronJobs;
    private readonly ImmutableArray<JobDefinition> oneTimeJobs;
    private readonly List<JobDefinition> allJobs;
    private readonly ImmutableArray<string> allJobTypeNames;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJobs = [..jobDefinitions];
        allJobTypeNames = [.. jobDefinitions.Select(j => j.JobFullName).Distinct()];
        cronJobs = [..jobDefinitions.Where(c => c.CronExpression is not null)];
        oneTimeJobs = [..jobDefinitions.Where(c => c.IsStartupJob)];
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;
    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;
    public IReadOnlyCollection<string> GetAllJobTypes() => allJobTypeNames;

    public bool IsJobRegistered<T>() => allJobs.Exists(j => j.Type == typeof(T));

    public JobDefinition GetJobDefinition<T>() => allJobs.First(j => j.Type == typeof(T));

    public void Add(JobDefinition jobDefinition)
    {
        if (!allJobs.Exists(j => j.JobFullName == jobDefinition.JobFullName))
        {
            allJobs.Add(jobDefinition);
        }
    }
    public int GetJobTypeConcurrencyLimit(string jobTypeName) => cronJobs.FirstOrDefault(j => j.JobFullName == jobTypeName)?.ConcurrencyPolicy?.MaxDegreeOfParallelism ?? 1;
}
