namespace NCronJob;

/// <summary>
/// Context information for a job that was skipped due to a condition not being satisfied.
/// </summary>
public sealed class JobConditionContext
{
    internal JobConditionContext(JobRun jobRun)
    {
        JobRun = jobRun;
    }

    /// <summary>
    /// Gets the unique identifier for this execution attempt.
    /// </summary>
    public Guid CorrelationId => JobRun.CorrelationId;

    /// <summary>
    /// Gets the custom name of the job, if one was specified during registration.
    /// </summary>
    public string? JobName => JobRun.JobDefinition.CustomName;

    /// <summary>
    /// Gets the type of the job that was skipped.
    /// </summary>
    public Type? JobType => JobRun.JobDefinition.Type;

    /// <summary>
    /// Gets the trigger type that initiated this job run.
    /// </summary>
    public TriggerType TriggerType => JobRun.TriggerType;

    /// <summary>
    /// Gets the parameter passed to the job, if any.
    /// </summary>
    public object? Parameter => JobRun.Parameter;

    internal JobRun JobRun { get; }
}

/// <summary>
/// Interface for handling notifications when a job's conditional execution check fails.
/// </summary>
/// <remarks>
/// This handler is invoked when a job's OnlyIf condition returns false, preventing job instantiation and execution.
/// The condition is evaluated once before job instantiation and is NOT re-evaluated during retry attempts.
/// </remarks>
public interface IJobConditionHandler
{
    /// <summary>
    /// Handles the notification that a job was skipped due to its condition not being satisfied.
    /// </summary>
    /// <param name="context">Context information about the skipped job.</param>
    /// <param name="cancellationToken">Token to cancel the notification handling.</param>
    /// <returns>A task representing the asynchronous notification handling.</returns>
    Task HandleConditionNotMetAsync(JobConditionContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Typed interface for handling notifications when a specific job's conditional execution check fails.
/// </summary>
/// <typeparam name="TJob">The type of job this handler is for.</typeparam>
public interface IJobConditionHandler<TJob> : IJobConditionHandler
    where TJob : IJob;
