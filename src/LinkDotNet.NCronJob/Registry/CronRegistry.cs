using System.Collections.Frozen;
using System.Collections.Immutable;

namespace LinkDotNet.NCronJob;

internal sealed class CronRegistry : IInstantJobRegistry
{
    private readonly FrozenSet<RegistryEntry> cronJobs;
    private readonly List<RegistryEntry> instantJobs = [];

    public CronRegistry(IEnumerable<RegistryEntry> jobs)
    {
        cronJobs = jobs.Where(c => c.CrontabSchedule is not null).ToFrozenSet();
    }

    public IReadOnlyCollection<RegistryEntry> GetAllCronJobs() => cronJobs;

    public IReadOnlyCollection<RegistryEntry> GetAllInstantJobsAndClear()
    {
        var returnedJobs = instantJobs.ToImmutableArray();
        instantJobs.Clear();
        return returnedJobs;
    }

    /// <inheritdoc />
    public void AddInstantJob<TJob>(object? parameter = null, IsolationLevel level = IsolationLevel.None)
        where TJob : IJob
    {
        instantJobs.Add(new RegistryEntry(typeof(TJob), new JobExecutionContext(parameter), level, null));
    }
}
