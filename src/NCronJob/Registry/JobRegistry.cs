using System.Collections.Immutable;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly List<JobDefinition> cronJobs;
    private readonly ImmutableArray<JobDefinition> oneTimeJobs;
    private readonly List<JobDefinition> allJob;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJob = [..jobDefinitions];
        cronJobs = [..jobDefinitions.Where(c => c.CronExpression is not null)];
        oneTimeJobs = [..jobDefinitions.Where(c => c.IsStartupJob)];

        AssertNoDuplicateJobNames();
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;

    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;

    public bool IsJobRegistered<T>() => allJob.Exists(j => j.Type == typeof(T));

    public JobDefinition? FindJobDefinition(string jobName)
        => allJob.Find(j => j.CustomName == jobName);

    public JobDefinition GetJobDefinitionForInstantJob<T>()
    {
        var definition = allJob.First(j => j.Type == typeof(T));
        if (definition.CronExpression is not null)
        {
            var instantDefinition = definition.CreateInstantVersion();
            allJob.Add(instantDefinition);
            return instantDefinition;
        }

        return definition;
    }

    public void Add(JobDefinition jobDefinition)
    {
        if (allJob.Exists(j => j.JobFullName == jobDefinition.JobFullName))
        {
            return;
        }

        AssertNoDuplicateJobNames(jobDefinition.CustomName);

        allJob.Add(jobDefinition);
        if (jobDefinition.CronExpression is not null)
        {
            cronJobs.Add(jobDefinition);
        }
    }

    public void RemoveByName(string jobName) => Remove(allJob.Find(j => j.CustomName == jobName));

    public void RemoveByType(Type type) => Remove(allJob.Find(j => j.Type == type));

    private void Remove(JobDefinition? jobDefinition)
    {
        if (jobDefinition is null)
        {
            return;
        }

        allJob.Remove(jobDefinition);
        if (jobDefinition.CronExpression is not null)
        {
            cronJobs.Remove(jobDefinition);
        }
    }

    private void AssertNoDuplicateJobNames(string? additionalJobName = null)
    {
        var duplicateJobName = allJob
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
