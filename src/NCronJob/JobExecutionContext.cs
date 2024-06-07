
namespace NCronJob;

/// <summary>
/// Represents the context of a job execution.
/// </summary>
public sealed record JobExecutionContext
{
    internal bool ExecuteChildren = true;

    /// <summary>
    /// Represents the context of a job execution. Marked internal to prevent external instantiation.
    /// </summary>
    /// <param name="jobRun">The Job Run.</param>
    internal JobExecutionContext(JobRun jobRun) => JobRun = jobRun;

    /// <summary>
    /// Represents the context of a job execution.
    /// </summary>
    [Obsolete("This will be removed in future versions to prevent external instantiation.", false)]
    public JobExecutionContext(Type jobType, object? parameter)
        => JobRun = JobRun.Create(new JobDefinition(jobType, parameter, null, null));

    /// <summary>
    /// The Job Instance Identifier, generated once upon creation of the context.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The output of a job that can be read by the <see cref="IJobNotificationHandler{TJob}"/>.
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// The attempts made to execute the job for one run. Will be incremented when a retry is triggered.
    /// Retries will only occur when <see cref="RetryPolicyAttribute{T}"/> is set on the Job.
    /// </summary>
    public int Attempts { get; internal set; }

    /// <summary>The Job Run instance.</summary>
    internal JobRun JobRun { get; }

    /// <summary>The Type that represents the Job</summary>
    internal Type JobType => JobRun.JobDefinition.Type;

    /// <summary>The passed in parameters to a job.</summary>
    public object? Parameter => JobRun.Parameter;

    /// <summary>
    /// The correlation identifier of the job run. The <see cref="CorrelationId"/> stays the same for jobs and their dependencies.
    /// </summary>
    public Guid CorrelationId => JobRun.CorrelationId;

    /// <summary>
    /// The output of the parent job. Is always <c>null</c> if the job was not started due to a dependency.
    /// </summary>
    public object? ParentOutput => JobRun.ParentOutput;

    /// <summary>
    /// Prohibits the execution of dependent jobs.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="SkipChildren"/> has no effects when no dependent jobs are defined
    /// via <see cref="INotificationStage{TJob}.ExecuteWhen"/>.
    /// </remarks>
    public void SkipChildren() => ExecuteChildren = false;
}
