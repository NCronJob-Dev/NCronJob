using System.Collections.Immutable;

namespace LinkDotNet.NCronJob;

internal sealed class CronRegistry : IInstantJobRegistry
{
    private readonly ImmutableArray<CronRegistryEntry> cronJobs;
    private readonly List<InstantEntry> instantJobs = [];

    public CronRegistry(IEnumerable<CronRegistryEntry> cronJobs)
    {
        this.cronJobs = cronJobs.ToImmutableArray();
    }

    public IEnumerable<CronRegistryEntry> GetAllCronJobs() => cronJobs;

    public IEnumerable<InstantEntry> GetAllInstantJobsAndClear()
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
