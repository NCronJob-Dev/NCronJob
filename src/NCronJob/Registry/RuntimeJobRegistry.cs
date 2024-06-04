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

    /// <summary>
    /// Removes the job with the given name.
    /// </summary>
    /// <param name="jobName">The name of the job to remove.</param>
    /// <remarks>If the given job is not found, no exception is thrown.</remarks>
    void RemoveJob(string jobName);

    /// <summary>
    /// Removes all jobs of the given type.
    /// </summary>
    void RemoveJob<TJob>() where TJob : IJob;

    /// <summary>
    /// Removes all jobs of the given type.
    /// </summary>
    void RemoveJob(Type type);

    /// <summary>
    /// Updates the schedule of a given job by its name.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="cronExpression">The new cron expression for the job.</param>
    /// <param name="timeZoneInfo">An optional timezone to use for the cron expression.If not provided, UTC is used.</param>
    /// <remarks>If the given job is not found, an exception is thrown.</remarks>
    void UpdateSchedule(string jobName, string cronExpression, TimeZoneInfo? timeZoneInfo = null);
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

    public void RemoveJob(string jobName)
    {
        jobRegistry.RemoveByName(jobName);
        jobQueue.ReevaluateQueue();
    }

    public void RemoveJob<TJob>() where TJob : IJob => RemoveJob(typeof(TJob));

    public void RemoveJob(Type type)
    {
        jobRegistry.RemoveByType(type);
        jobQueue.ReevaluateQueue();
    }

    public void UpdateSchedule(string jobName, string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(cronExpression);

        var precisionRequired = JobOptionBuilder.DetermineAndValidatePrecision(cronExpression, null);
        var job = jobRegistry.GetJobDefinition(jobName);

        var cron = NCronJobOptionBuilder.GetCronExpression(cronExpression, precisionRequired);

        job.CronExpression = cron;
        job.TimeZone = timeZoneInfo ?? TimeZoneInfo.Utc;

        jobQueue.ReevaluateQueue();
    }
}
