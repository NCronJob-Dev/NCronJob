using System.Diagnostics.CodeAnalysis;
using Cronos;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Gives the ability to add, delete or adjust jobs at runtime.
/// </summary>
public interface IRuntimeJobRegistry
{
    /// <summary>
    /// Tries to register a job with the given configuration.
    /// </summary>/param>
    /// <param name="jobBuilder">The job builder that configures the job.</param>
    /// <returns>Returns <c>true</c> if the registration was successful, otherwise <c>false</c>.</returns>
    bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder);

    /// <summary>
    /// Tries to register a job with the given configuration.
    /// </summary>/param>
    /// <param name="jobBuilder">The job builder that configures the job.</param>
    /// <param name="exception">The exception that occurred during the registration process. Or <c>null</c> if the registration was successful.</param>
    /// <returns>Returns <c>true</c> if the registration was successful, otherwise <c>false</c>.</returns>
    bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder, [NotNullWhen(false)]out Exception? exception);

    /// <summary>
    /// Removes the job with the given name.
    /// </summary>
    /// <param name="jobName">The name of the job to remove.</param>
    /// <remarks>If the given job is not found, no exception is thrown.</remarks>
    void RemoveJob(string jobName);

    /// <summary>
    /// Removes all jobs of the given type.
    /// </summary>
    /// <remarks>If the given job is not found, no exception is thrown.</remarks>
    void RemoveJob<TJob>() where TJob : IJob;

    /// <summary>
    /// Removes all jobs of the given type.
    /// </summary>
    /// <remarks>If the given job is not found, no exception is thrown.</remarks>
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
    /// Enables a job that was previously disabled.
    /// </summary>
    /// <param name="jobName">The unique job name that identifies this job.</param>
    /// <remarks>
    /// If the job is already enabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void EnableJob(string jobName);

    /// <summary>
    /// Enables all jobs of the given type that were previously disabled.
    /// </summary>
    /// <remarks>
    /// If the job is already enabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void EnableJob<TJob>() where TJob : IJob;

    /// <summary>
    /// Enables all jobs of the given type that were previously disabled.
    /// </summary>
    /// <remarks>
    /// If the job is already enabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void EnableJob(Type type);

    /// <summary>
    /// Disables a job that was previously enabled.
    /// </summary>
    /// <param name="jobName">The unique job name that identifies this job.</param>
    /// <remarks>
    /// If the job is already disabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void DisableJob(string jobName);

    /// <summary>
    /// Disables all jobs of the given type.
    /// </summary>
    /// <remarks>
    /// If the job is already disabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void DisableJob<TJob>() where TJob : IJob;

    /// <summary>
    /// Disables all jobs of the given type.
    /// </summary>
    /// <remarks>
    /// If the job is already disabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void DisableJob(Type type);
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
    // https://crontab.guru/#*_*_31_2_*
    internal static readonly CronExpression TheThirtyFirstOfFebruary = CronExpression.Parse("* * 31 2 *");

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
    public bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder)
        => TryRegister(jobBuilder, out _);

    /// <inheritdoc />
    public bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder, [NotNullWhen(false)]out Exception? exception)
    {
        try
        {
            var oldJobs = jobRegistry.GetAllJobs();
            var builder = new NCronJobOptionBuilder(services, concurrencySettings, jobRegistry);
            jobBuilder(builder);

            var newJobs = jobRegistry.GetAllJobs().Except(oldJobs);
            foreach (var jobDefinition in newJobs)
            {
                jobWorker.ScheduleJob(jobDefinition);
                jobQueueManager.SignalJobQueue(jobDefinition.JobFullName);
            }

            foreach (var entry in jobRegistry.DynamicJobRegistrations)
            {
                jobQueueManager.SignalJobQueue(entry.JobDefinition.JobFullName);
            }
            exception = null;
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
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
        job.UserDefinedCronExpression = cronExpression;
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

        EnableJob(job, jobName);
    }

    /// <inheritdoc />
    public void EnableJob<TJob>() where TJob : IJob => EnableJob(typeof(TJob));

    /// <inheritdoc />
    public void EnableJob(Type type)
    {
        ProcessAllJobDefinitionsOfType(type, j => EnableJob(j));
    }

    /// <inheritdoc />
    public void DisableJob(string jobName)
    {
        var job = jobRegistry.FindJobDefinition(jobName)
                  ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

        DisableJob(job, jobName);
    }

    /// <inheritdoc />
    public void DisableJob<TJob>() where TJob : IJob => DisableJob(typeof(TJob));

    /// <inheritdoc />
    public void DisableJob(Type type)
    {
        ProcessAllJobDefinitionsOfType(type, j=> DisableJob(j));
    }

    private void ProcessAllJobDefinitionsOfType(Type type, Action<JobDefinition> processor)
    {
        var jobDefinitions = jobRegistry.FindAllJobDefinition(type);

        foreach (var jobDefinition in jobDefinitions)
        {
            processor(jobDefinition);
        }
    }

    private void EnableJob(JobDefinition job, string? customName = null)
    {
        if (job.UserDefinedCronExpression is not null)
        {
            job.CronExpression = CronExpression.Parse(job.UserDefinedCronExpression);
        }
        else
        {
            job.CronExpression = null;
        }

        RescheduleJob(job, customName);
    }

    private void DisableJob(JobDefinition job, string? customName = null)
    {
        // Scheduling on Feb, 31st is a sure way to never get it to run
        job.CronExpression = TheThirtyFirstOfFebruary;

        RescheduleJob(job, customName);
    }

    private void RescheduleJob(JobDefinition job, string? customName = null)
    {
        if (customName is not null)
        {
            jobWorker.RescheduleJobWithJobName(job);
            return;
        }

        jobWorker.RescheduleJobByType(job);
    }
}
