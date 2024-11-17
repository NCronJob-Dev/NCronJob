namespace NCronJob;

/// <summary>
/// Represents the context of a job execution.
/// </summary>
public interface IJobExecutionContext
{
    /// <summary>
    /// The Job Instance Identifier, generated once upon creation of the context.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The output of a job that can be read by the <see cref="IJobNotificationHandler{TJob}"/>.
    /// </summary>
    object? Output { get; set; }

    /// <summary>
    /// The attempts made to execute the job for one run. Will be incremented when a retry is triggered.
    /// Retries will only occur when <see cref="RetryPolicyAttribute{T}"/> is set on the Job.
    /// </summary>
    int Attempts { get; }

    /// <summary>The passed in parameters to a job.</summary>
    object? Parameter { get; }

    /// <summary>
    /// The correlation identifier of the job run. The <see cref="CorrelationId"/> stays the same for jobs and their dependencies.
    /// </summary>
    Guid CorrelationId { get; }

    /// <summary>
    /// The output of the parent job. Is always <c>null</c> if the job was not started due to a dependency.
    /// </summary>
    object? ParentOutput { get; }

    /// <summary>
    /// The custom name of the job given by the user. If not set, it will be <c>null</c>.
    /// </summary>
    public string? JobName { get; }

    /// <summary>
    /// The type that represents the job. Is <c>null</c> if the job is an anonymous job.
    /// </summary>
    public Type? JobType { get; }

    /// <summary>
    /// Prohibits the execution of dependent jobs.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="JobExecutionContext.SkipChildren"/> has no effects when no dependent jobs are defined
    /// via <see cref="INotificationStage{TJob}.ExecuteWhen"/>.
    /// </remarks>
    public void SkipChildren();
}
