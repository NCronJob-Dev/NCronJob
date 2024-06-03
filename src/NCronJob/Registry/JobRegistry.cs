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
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;
    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => oneTimeJobs;

    public bool IsJobRegistered<T>() => allJob.Exists(j => j.Type == typeof(T));

    public JobDefinition GetJobDefinition<T>() => allJob.First(j => j.Type == typeof(T));

    public void Add(JobDefinition jobDefinition)
    {
        if (!allJob.Exists(j => j.JobFullName == jobDefinition.JobFullName))
        {
            allJob.Add(jobDefinition);
            if (jobDefinition.CronExpression is not null)
            {
                cronJobs.Add(jobDefinition);
            }
        }
    }
}
