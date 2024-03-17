namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a job that can be executed.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Entry point for the job. This method will be called when the job is executed.
    /// </summary>
    /// <param name="token">A cancellation token that can be used to cancel the job.</param>
    Task Run(CancellationToken token = default);
}
