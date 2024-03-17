namespace LinkDotNet.NCronJob;

internal sealed class CronRegistry : IInstantJobRegistry
{
    private readonly List<RegistryEntry> cronJobs = [];

    public CronRegistry(IEnumerable<CronRegistryEntry> cronJobs)
    {
        this.cronJobs.AddRange(cronJobs);
    }

    public IEnumerable<CronRegistryEntry> GetAllCronJobs() => cronJobs.OfType<CronRegistryEntry>();

    public IEnumerable<InstantEntry> GetAllInstantJobs() => cronJobs.OfType<InstantEntry>();

    /// <inheritdoc />
    public void AddInstantJob<TJob>() where TJob : IJob
    {
        cronJobs.Add(new InstantEntry(typeof(TJob)));
    }
}
