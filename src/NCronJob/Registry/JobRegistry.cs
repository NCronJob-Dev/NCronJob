using System.Collections.Immutable;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly List<JobDefinition> allJobs;
    private ImmutableArray<JobDefinition> cronJobs;
    private ImmutableArray<JobDefinition> oneTimeJobs;
    private ImmutableArray<string> allJobTypeNames;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJobs = [..jobDefinitions];
        RefreshImmutableArrays();
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;
    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;
    public IReadOnlyCollection<string> GetAllJobTypes() => allJobTypeNames;

    public bool IsJobRegistered<T>() => allJobs.Exists(j => j.Type == typeof(T));

    public void Add(JobDefinition jobDefinition)
    {
        if (!allJobs.Exists(j => j.JobFullName == jobDefinition.JobFullName))
        {
            allJobs.Add(jobDefinition);
            RefreshImmutableArrays();
        }
    }
    public int GetJobTypeConcurrencyLimit(string jobTypeName) => allJobs.Find(j => j.JobFullName == jobTypeName)?.ConcurrencyPolicy?.MaxDegreeOfParallelism ?? 1;

    private void RefreshImmutableArrays()
    {
        allJobTypeNames = [.. allJobs.Select(j => j.JobFullName).Distinct()];
        cronJobs = [.. allJobs.Where(c => c.CronExpression is not null)];
        oneTimeJobs = [.. allJobs.Where(c => c.IsStartupJob)];
    }
}
