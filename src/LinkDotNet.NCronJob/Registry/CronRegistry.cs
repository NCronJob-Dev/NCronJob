using System.Collections.Frozen;

namespace LinkDotNet.NCronJob;

internal sealed class CronRegistry : IInstantJobRegistry
{
    private readonly FrozenSet<CronRegistryEntry> cronJobs;
    private readonly List<InstantEntry> instantJobs = [];

    public CronRegistry(IEnumerable<CronRegistryEntry> cronJobs)
    {
        this.cronJobs = cronJobs.ToFrozenSet();
    }

    public IReadOnlyCollection<CronRegistryEntry> GetAllCronJobs() => cronJobs;

    public IReadOnlyCollection<InstantEntry> GetAllInstantJobsAndClear()
    {
        var returnedJobs = instantJobs.ToArray();
        instantJobs.Clear();
        return returnedJobs;
    }

    /// <inheritdoc />
    public void AddInstantJob<TJob>(object? parameter = null) where TJob : IJob
    {
        instantJobs.Add(new InstantEntry(typeof(TJob), new JobExecutionContext(parameter)));
    }
}
