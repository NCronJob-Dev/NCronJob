using System.Collections.Immutable;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly ImmutableArray<JobDefinition> cronJobs;
    private readonly ImmutableArray<JobDefinition> oneTimeJobs;
    private readonly ImmutableArray<JobDefinition> allJob;

    public JobRegistry(IEnumerable<JobDefinition> jobs)
    {
        var jobDefinitions = jobs as JobDefinition[] ?? jobs.ToArray();
        allJob = [..jobDefinitions];
        cronJobs = [..jobDefinitions.Where(c => c.CronExpression is not null)];
        oneTimeJobs = [..jobDefinitions.Where(c => c.IsStartupJob)];
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;
    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;

    public bool IsJobRegistered<T>() => allJob.Any(j => j.Type == typeof(T));

    public JobDefinition GetJobDefinition<T>() => allJob.First(j => j.Type == typeof(T));
}

