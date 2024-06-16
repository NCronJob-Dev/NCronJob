using System.Collections.Immutable;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly List<JobDefinition> allJobs;
    private readonly ImmutableArray<JobDefinition> oneTimeJobs;
    private readonly List<JobDefinition> cronJobs;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJobs = [..jobDefinitions];
        cronJobs = [..jobDefinitions.Where(c => c.CronExpression is not null)];
        oneTimeJobs = [..jobDefinitions.Where(c => c.IsStartupJob)];

        AssertNoDuplicateJobNames();
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;
    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;

    public bool IsJobRegistered<T>() => allJobs.Exists(j => j.Type == typeof(T));

    public JobDefinition GetJobDefinition<T>() => allJobs.First(j => j.Type == typeof(T));

    public JobDefinition? FindJobDefinition(Type type)
        => allJobs.Find(j => j.Type == type);

    public JobDefinition? FindJobDefinition(string jobName)
        => allJobs.Find(j => j.CustomName == jobName);

    public void Add(JobDefinition jobDefinition)
    {
        AssertNoDuplicateJobNames(jobDefinition.CustomName);

        var isTypeUpdate = allJobs.Exists(j => j.JobFullName == jobDefinition.JobFullName);
        if (isTypeUpdate)
        {
            Remove(jobDefinition);
        }

        allJobs.Add(jobDefinition);
        if (jobDefinition.CronExpression is not null)
        {
            cronJobs.Add(jobDefinition);
        }
    }
    public int GetJobTypeConcurrencyLimit(string jobTypeName)
        => allJobs.Find(j => j.JobFullName == jobTypeName)
            ?.ConcurrencyPolicy
            ?.MaxDegreeOfParallelism ?? 1;

    public void RemoveByName(string jobName) => Remove(allJobs.Find(j => j.CustomName == jobName));

    public void RemoveByType(Type type) => Remove(allJobs.Find(j => j.Type == type));

    private void Remove(JobDefinition? jobDefinition)
    {
        if (jobDefinition is null)
        {
            return;
        }

        allJobs.Remove(jobDefinition);
        if (jobDefinition.CronExpression is not null)
        {
            cronJobs.Remove(jobDefinition);
        }
    }

    private void AssertNoDuplicateJobNames(string? additionalJobName = null)
    {
        var duplicateJobName = allJobs
            .Select(c => c.CustomName)
            .Concat([additionalJobName])
            .Where(s => s is not null)
            .GroupBy(s => s)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateJobName is not null)
        {
            throw new InvalidOperationException($"Duplicate job names found: {string.Join(", ", duplicateJobName)}");
        }
    }
}
