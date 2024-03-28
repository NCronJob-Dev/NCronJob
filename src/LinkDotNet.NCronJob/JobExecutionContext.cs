namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents the context of a job execution.
/// </summary>
/// <param name="Parameter">The passed in parameters to a job.</param>
public sealed record JobExecutionContext(object? Parameter)
{
    /// <summary>
    /// The output of a job that can be read by the <see cref="IJobNotificationHandler{TJob}"/>.
    /// </summary>
    public object? Output { get; set; }
}
