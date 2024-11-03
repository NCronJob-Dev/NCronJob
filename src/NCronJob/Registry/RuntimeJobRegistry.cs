using Cronos;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Gives the ability to add, delete or adjust jobs at runtime.
/// </summary>
public interface IRuntimeJobRegistry
{
    /// <summary>
    /// Gives the ability to add a job.
    /// </summary>
    void Register(Action<IRuntimeJobBuilder> jobBuilder);

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
/// <param name="JobName">The job name given by the user.</param>
/// <param name="CronExpression">The cron expression that defines when the job should be executed.</param>
/// <param name="TimeZone">The timezone that is used to evaluate the cron expression.</param>
public sealed record RecurringJobSchedule(string? JobName, string CronExpression, TimeZoneInfo TimeZone);

/// <inheritdoc />
internal sealed class RuntimeJobRegistry : IRuntimeJobRegistry
{
    private readonly IServiceCollection services;
    private readonly JobRegistry jobRegistry;
    private readonly JobWorker jobWorker;
    private readonly JobQueueManager jobQueueManager;
    private readonly ConcurrencySettings concurrencySettings;

    public RuntimeJobRegistry(
        IServiceCollection services,
        JobRegistry jobRegistry,
        JobWorker jobWorker,
        JobQueueManager jobQueueManager,
        ConcurrencySettings concurrencySettings)
    {
        this.services = services;
        this.jobRegistry = jobRegistry;
        this.jobWorker = jobWorker;
        this.jobQueueManager = jobQueueManager;
        this.concurrencySettings = concurrencySettings;
    }

    /// <inheritdoc />
    public void Register(Action<IRuntimeJobBuilder> jobBuilder)
    {
        var builder = new NCronJobOptionBuilder(services, concurrencySettings, jobRegistry);
        jobBuilder(builder);

        foreach (var jobDefinition in jobRegistry.GetAllJobs())
        {
            jobWorker.ScheduleJob(jobDefinition);
            jobQueueManager.SignalJobQueue(jobDefinition.JobFullName);
        }

        foreach (var entry in jobRegistry.DynamicJobRegistrations)
        {
            jobQueueManager.SignalJobQueue(entry.JobDefinition.JobFullName);
        }
    }

    /// <inheritdoc />
    public void RemoveJob(string jobName) => jobWorker.RemoveJobByName(jobName);

    /// <inheritdoc />
    public void RemoveJob<TJob>() where TJob : IJob => RemoveJob(typeof(TJob));

    /// <inheritdoc />
    public void RemoveJob(Type type) => jobWorker.RemoveJobType(type);

    /// <inheritdoc />
    public void UpdateSchedule(string jobName, string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(cronExpression);

        var job = jobRegistry.FindJobDefinition(jobName) ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        var cron = NCronJobOptionBuilder.GetCronExpression(cronExpression);

        job.CronExpression = cron;
        job.TimeZone = timeZoneInfo ?? TimeZoneInfo.Utc;

        jobWorker.RescheduleJobWithJobName(job);
    }

    /// <inheritdoc />
    public void UpdateParameter(string jobName, object? parameter)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        var job = jobRegistry.FindJobDefinition(jobName) ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");
        job.Parameter = parameter;

        jobWorker.RescheduleJobWithJobName(job);
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

        cronExpression = job.UserDefinedCronExpression;
        timeZoneInfo = job.TimeZone;

        return true;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<RecurringJobSchedule> GetAllRecurringJobs()
        => jobRegistry
            .GetAllCronJobs()
            .Select(s => new RecurringJobSchedule(
                s.CustomName,
                s.UserDefinedCronExpression!,
                s.TimeZone!))
            .ToArray();

    /// <inheritdoc />
    public void EnableJob(string jobName)
    {
        var job = jobRegistry.FindJobDefinition(jobName)
                  ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        if (job.CronExpression is not null)
        {
            job.CronExpression = CronExpression.Parse(job.UserDefinedCronExpression);
        }

        jobWorker.RescheduleJobWithJobName(job);
    }

    /// <inheritdoc />
    public void DisableJob(string jobName)
    {
        var job = jobRegistry.FindJobDefinition(jobName)
                  ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        if (job.CronExpression is not null)
        {
            // https://crontab.guru/#*_*_31_2_*
            // Scheduling on Feb, 31st is a sure way to never get it to run
            job.CronExpression = CronExpression.Parse("* * 31 2 *");
        }

        jobWorker.RescheduleJobWithJobName(job);
    }
}
