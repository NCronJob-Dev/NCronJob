using System.Diagnostics.CodeAnalysis;

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
    /// <param name="exception">The exception that occurred during the registration process. Or <c>null</c> if the registration was successful.</param>
    /// <returns>Returns <c>true</c> if the registration was successful, otherwise <c>false</c>.</returns>
    bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder, [NotNullWhen(false)] out Exception? exception);

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
    /// <param name="timeZoneInfo">The associated time zone. If the job has no schedule, or couldn't be found this will be <c>null</c>.</param>
    /// <returns>Returns <c>true</c> if the job was found, otherwise <c>false</c>.</returns>
    bool TryGetSchedule(string jobName, out string? cronExpression, out TimeZoneInfo? timeZoneInfo);

    /// <summary>
    /// Tries to retrieve the next scheduled occurrence of an enabled recurring job by its name.
    /// </summary>
    /// <param name="jobName">The given job name.</param>
    /// <param name="nextRun">
    /// The next occurrence in UTC, or <c>null</c> when a valid recurring schedule has no future occurrence.
    /// This is also <c>null</c> when the method returns <c>false</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when an enabled recurring job was found; otherwise <c>false</c> for unknown, disabled,
    /// or unscheduled jobs.
    /// </returns>
    bool TryGetNextOccurrence(string jobName, out DateTimeOffset? nextRun);

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
    void DisableJob(Type type);
}
