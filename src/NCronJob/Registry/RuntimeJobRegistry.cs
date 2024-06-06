using Cronos;

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
    /// <remarks>
    /// If the given job is not found, an exception is thrown.
    /// Furthermore, all current planned executions of that job are canceled and rescheduled with the new cron expression.
    /// </remarks>
    void UpdateSchedule(string jobName, string cronExpression, TimeZoneInfo? timeZoneInfo = null);

    /// <summary>
    /// Updates the parameter of a given job by its name.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="parameter">The new parameter that will be passed into the job.</param>
    /// <remarks>
    /// If the given job is not found, an exception is thrown.
    /// Furthermore, all current planned executions of that job are canceled and rescheduled with the new parameter.
    /// </remarks>
    void UpdateParameter(string jobName, object? parameter);

    /// <summary>
    /// Retrieves the schedule of a given job by its name. If the job is not found, the out parameters are set to null.
    /// </summary>
    /// <param name="jobName">The given job name.</param>
    /// <param name="cronExpression">The associated cron expression. If the job has none, or couldn't be found this will be <c>null</c>.</param>
    /// <param name="timeZoneInfo">The associated time zone. If the job has none, or couldn't be found this will be <c>null</c>.</param>
    /// <returns>Returns <c>true</c> if the job was found, otherwise <c>false</c>.</returns>
    bool TryGetSchedule(string jobName, out string? cronExpression, out TimeZoneInfo? timeZoneInfo);

    /// <summary>
    /// Returns a list of all recurring jobs.
    /// </summary>
    /// <returns></returns>
    IReadOnlyCollection<RecurringJobSchedule> GetAllRecurringJobs();

    /// <summary>
    /// This will enable a job that was previously disabled.
    /// </summary>
    /// <param name="jobName">The unique job name that identifies this job.</param>
    /// <remarks>
    /// If the job is already enabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void EnableJob(string jobName);

    /// <summary>
    /// This will disable a job that was previously enabled.
    /// </summary>
    /// <param name="jobName">The unique job name that identifies this job.</param>
    /// <remarks>
    /// If the job is already disabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void DisableJob(string jobName);
}

/// <summary>
/// Represents a recurring job schedule.
/// </summary>
/// <param name="JobType">The associated job type or <c>null</c> if the job is an anonymous job.</param>
/// <param name="JobName">The job name given by the user.</param>
/// <param name="CronExpression">The cron expression that defines when the job should be executed.</param>
/// <param name="TimeZone">The timezone that is used to evaluate the cron expression.</param>
public sealed record RecurringJobSchedule(Type? JobType, string? JobName, string CronExpression, TimeZoneInfo TimeZone);

/// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void RemoveJob(string jobName)
    {
        jobRegistry.RemoveByName(jobName);
        jobQueue.ReevaluateQueue();
    }

    /// <inheritdoc />
    public void RemoveJob<TJob>() where TJob : IJob => RemoveJob(typeof(TJob));

    /// <inheritdoc />
    public void RemoveJob(Type type)
    {
        jobRegistry.RemoveByType(type);
        jobQueue.ReevaluateQueue();
    }

    /// <inheritdoc />
    public void UpdateSchedule(string jobName, string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(cronExpression);

        var precisionRequired = JobOptionBuilder.DetermineAndValidatePrecision(cronExpression, null);
        var job = jobRegistry.FindJobDefinition(jobName) ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        var cron = NCronJobOptionBuilder.GetCronExpression(cronExpression, precisionRequired);

        job.CronExpression = cron;
        job.TimeZone = timeZoneInfo ?? TimeZoneInfo.Utc;

        jobQueue.ReevaluateQueue();
    }

    /// <inheritdoc />
    public void UpdateParameter(string jobName, object? parameter)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        var job = jobRegistry.FindJobDefinition(jobName) ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        job.Parameter = parameter;

        jobQueue.ReevaluateQueue();
    }

    /// <inheritdoc />
    public bool TryGetSchedule(string jobName, out string? cronExpression, out TimeZoneInfo? timeZoneInfo)
    {
        cronExpression = null;
        timeZoneInfo = null;

        var job = jobRegistry.FindJobDefinition(jobName);
        if (job is null)
        {
            return false;
        }

        cronExpression = job.CronExpressionString;
        timeZoneInfo = job.TimeZone;

        return true;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<RecurringJobSchedule> GetAllRecurringJobs()
        => jobRegistry
            .GetAllCronJobs()
            .Select(s => new RecurringJobSchedule(
                s.Type == typeof(DynamicJobFactory) ? null : s.Type,
                s.CustomName,
                s.CronExpressionString!,
                s.TimeZone!))
            .ToArray();

    /// <inheritdoc />
    public void EnableJob(string jobName)
    {
        var job = jobRegistry.FindJobDefinition(jobName)
                  ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        if (job.CronExpression is not null)
        {
            job.CronExpression = CronExpression.Parse(job.CronExpressionString);
            jobQueue.ReevaluateQueue();
        }
    }

    /// <inheritdoc />
    public void DisableJob(string jobName)
    {
        var job = jobRegistry.FindJobDefinition(jobName)
                  ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        if (job.CronExpression is not null)
        {
            job.CronExpression = CronExpression.Parse("* * 31 2 *");
            jobQueue.ReevaluateQueue();
        }
    }
}
