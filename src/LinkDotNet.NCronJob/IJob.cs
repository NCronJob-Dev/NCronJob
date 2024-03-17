namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a job that can be executed.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Entry point for the job. This method will be called when the job is executed.
    /// </summary>
    /// <param name="context">The context of the job execution.</param>
    /// <param name="token">A cancellation token that can be used to cancel the job.</param>
    Task Run(JobExecutionContext context, CancellationToken token = default);
}
