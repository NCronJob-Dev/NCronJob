namespace NCronJob;

/// <summary>
/// Gives the ability to add, delete or adjust jobs at runtime.
/// </summary>
public interface IRuntimeJobRegistry
{
    /// <summary>
    /// Gives the ability to add a job.
    /// </summary>
    void AddJob(Action<NCronJobOptionBuilder> jobBuilder);
}

internal sealed class RuntimeJobRegistry : IRuntimeJobRegistry
{
    private readonly JobRegistry jobRegistry;
    private readonly JobQueue jobQueue;
    private readonly DynamicJobFactoryRegistry dynamicJobFactoryRegistry;
    private readonly ConcurrencySettings concurrencySettings;

    public RuntimeJobRegistry(
        JobRegistry jobRegistry,
        JobQueue jobQueue,
        DynamicJobFactoryRegistry dynamicJobFactoryRegistry,
        ConcurrencySettings concurrencySettings)
    {
        this.jobRegistry = jobRegistry;
        this.jobQueue = jobQueue;
        this.dynamicJobFactoryRegistry = dynamicJobFactoryRegistry;
        this.concurrencySettings = concurrencySettings;
    }

    public void AddJob(Action<NCronJobOptionBuilder> jobBuilder)
    {
        var runtimeCollection = new RuntimeServiceCollection();
        var builder = new NCronJobOptionBuilder(runtimeCollection, concurrencySettings);
        jobBuilder(builder);

        foreach (var jobDefinition in runtimeCollection.GetJobDefinitions())
        {
            jobRegistry.Add(jobDefinition);
        }

        foreach (var entry in runtimeCollection.GetDynamicJobFactoryRegistries())
        {
            dynamicJobFactoryRegistry.Add(entry);
        }

        jobQueue.ReevaluateQueue();
    }
}
