namespace NCronJob;

/// <summary>
/// Represents a recurring job schedule.
/// </summary>
/// <param name="JobName">The optional custom name given to the job. Will be <c>null</c> when no name was specified.</param>
/// <param name="Type">The type of the job; Will be <c>null</c> if the job is an anonymous function based job.</param>
/// <param name="IsTypedJob">Whether the job is a class based job (implementing <see cref="IJob"/>) or not.</param>
/// <param name="CronExpression">The cron expression that defines when the job should be executed.</param>
/// <param name="IsEnabled">Whether the job is enabled or not.</param>
/// <param name="TimeZone">The timezone that is used to evaluate the cron expression.</param>
public sealed record RecurringJobSchedule(string? JobName, string CronExpression, bool IsEnabled, TimeZoneInfo TimeZone, Type? Type = null, bool IsTypedJob = true);
