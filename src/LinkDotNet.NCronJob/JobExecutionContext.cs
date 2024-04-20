namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents the context of a job execution.
/// </summary>
/// <param name="JobType">The Type that represents the Job</param>
/// <param name="Parameter">The passed in parameters to a job.</param>
public sealed record JobExecutionContext(Type JobType, object? Parameter)
{
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
}
