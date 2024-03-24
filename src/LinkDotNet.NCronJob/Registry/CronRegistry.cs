namespace LinkDotNet.NCronJob;

internal sealed class CronRegistry : IInstantJobRegistry
{
    private readonly List<RegistryEntry> jobs;

    public CronRegistry(IEnumerable<RegistryEntry> cronJobs)
    {
        jobs = cronJobs.ToList();
    }

    public IReadOnlyCollection<RegistryEntry> GetAllCronJobs() =>
        jobs.Where(j => j.CrontabSchedule is not null)
            .ToList();

    public IReadOnlyCollection<RegistryEntry> GetAllInstantJobsAndClear()
    {
        var returnedJobs = jobs.Where(j => j.CrontabSchedule is null).ToList();
        returnedJobs.ForEach(j => jobs.Remove(j));
        return returnedJobs;
    }

    /// <inheritdoc />
    public void AddInstantJob<TJob>(object? parameter = null, IsolationLevel level = IsolationLevel.None)
        where TJob : IJob
    {
        jobs.Add(new RegistryEntry(typeof(TJob), new JobExecutionContext(parameter), level, null));
    }
}
