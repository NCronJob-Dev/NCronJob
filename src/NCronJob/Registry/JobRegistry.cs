using System.Collections.Immutable;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly HashSet<JobDefinition> allJobs;
    private readonly ImmutableHashSet<JobDefinition> oneTimeJobs;
    private readonly HashSet<JobDefinition> cronJobs;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJobs = new HashSet<JobDefinition>(jobDefinitions, JobDefinitionEqualityComparer.Instance);
        cronJobs = new HashSet<JobDefinition>(jobDefinitions.Where(c => c.CronExpression is not null), JobDefinitionEqualityComparer.Instance);
        oneTimeJobs = [..jobDefinitions.Where(c => c.IsStartupJob)];

        AssertNoDuplicateJobNames();
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;

    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;

    public bool IsJobRegistered<T>() => allJobs.Any(j => j.Type == typeof(T));

    public JobDefinition GetJobDefinition<T>() => allJobs.First(j => j.Type == typeof(T));

    public JobDefinition? FindJobDefinition(Type type)
        => allJobs.FirstOrDefault(j => j.Type == type);

    public JobDefinition? FindJobDefinition(string jobName)
        => allJobs.FirstOrDefault(j => j.CustomName == jobName);

    public void Add(JobDefinition jobDefinition)
    {
        AssertNoDuplicateJobNames(jobDefinition.CustomName);

        var isTypeUpdate = allJobs.Any(j => j.JobFullName == jobDefinition.JobFullName);
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
        => allJobs.FirstOrDefault(j => j.JobFullName == jobTypeName)
            ?.ConcurrencyPolicy
            ?.MaxDegreeOfParallelism ?? 1;

    public void RemoveByName(string jobName) => Remove(allJobs.FirstOrDefault(j => j.CustomName == jobName));

    public void RemoveByType(Type type) => Remove(allJobs.FirstOrDefault(j => j.Type == type));

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

    private sealed class JobDefinitionEqualityComparer : IEqualityComparer<JobDefinition>
    {
        public static readonly JobDefinitionEqualityComparer Instance = new();

        public bool Equals(JobDefinition? x, JobDefinition? y) =>
            (x is null && y is null) || (x is not null && y is not null && x.Type == y.Type
                                         && x.Parameter == y.Parameter
                                         && x.CronExpression == y.CronExpression
                                         && x.TimeZone == y.TimeZone
                                         && x.CustomName == y.CustomName
                                         && x.IsStartupJob == y.IsStartupJob);

        public int GetHashCode(JobDefinition obj) => HashCode.Combine(
            obj.Type,
            obj.Parameter,
            obj.CronExpression,
            obj.TimeZone,
            obj.CustomName,
            obj.IsStartupJob);
    }
}
