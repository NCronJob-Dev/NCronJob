
namespace NCronJob;

/// <summary>
/// Represents the context of a job execution.
/// </summary>
public sealed record JobExecutionContext
{
    /// <summary>
    /// Represents the context of a job execution. Marked internal to prevent external instantiation.
    /// </summary>
    /// <param name="jobDefinition">The Job Definition of the Job Run</param>
    internal JobExecutionContext(JobDefinition jobDefinition)
    {
        Parameter = jobDefinition.Parameter;
        JobDefinition = jobDefinition;
    }

    /// <summary>
    /// Represents the context of a job execution.
    /// </summary>
    public JobExecutionContext(Type jobType, object? parameter)
        => JobDefinition = new JobDefinition(jobType, parameter, null, null);

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

    /// <summary>The Job Definition of the Job Run</summary>
    internal JobDefinition JobDefinition { get; init; }

    /// <summary>The Type that represents the Job</summary>
    internal Type JobType => JobDefinition.Type;

    /// <summary>The passed in parameters to a job.</summary>
    public object? Parameter { get; init; }
}
