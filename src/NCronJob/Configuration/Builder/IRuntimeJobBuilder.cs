namespace NCronJob;

/// <summary>
/// Represents a builder for adding jobs at runtime.
/// </summary>
public interface IRuntimeJobBuilder
{
    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    /// <returns>Returns the <see cref="IRuntimeJobBuilder"/> for chaining.</returns>
    IRuntimeJobBuilder AddJob<TJob>(Action<JobOptionBuilder>? options = null) where TJob : class, IJob;

    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobType">The type of the job to be added.</param>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    /// <returns>Returns the <see cref="IRuntimeJobBuilder"/> for chaining.</returns>
    IRuntimeJobBuilder AddJob(Type jobType, Action<JobOptionBuilder>? options = null);

    /// <summary>
    /// Adds a job using an asynchronous anonymous delegate to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed.</param>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="timeZoneInfo">The time zone information that the cron expression should be evaluated against.
    /// If not set the default time zone is UTC.
    /// </param>
    /// <param name="jobName">Sets the job name that can be used to identify and manipulate the job later on.</param>
    /// <returns>Returns the <see cref="IRuntimeJobBuilder"/> for chaining.</returns>
    IRuntimeJobBuilder AddJob(Delegate jobDelegate,
        string cronExpression,
        TimeZoneInfo? timeZoneInfo = null,
        string? jobName = null);
}
