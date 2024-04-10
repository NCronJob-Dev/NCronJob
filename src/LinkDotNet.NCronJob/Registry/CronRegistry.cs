using System.Collections.Frozen;

namespace LinkDotNet.NCronJob;

internal sealed class CronRegistry : IInstantJobRegistry
{
    private readonly JobExecutor jobExecutor;
    private readonly FrozenSet<RegistryEntry> cronJobs;

    public CronRegistry(IEnumerable<RegistryEntry> jobs, JobExecutor jobExecutor)
    {
        this.jobExecutor = jobExecutor;
        cronJobs = jobs.Where(c => c.CrontabSchedule is not null).ToFrozenSet();
    }

    public IReadOnlyCollection<RegistryEntry> GetAllCronJobs() => cronJobs;

    /// <inheritdoc />
    public void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob
    {
        var executionContext = new JobExecutionContext(parameter);
        var run = new RegistryEntry(typeof(TJob), executionContext, null);
        jobExecutor.RunJob(run, token);
    }
}
