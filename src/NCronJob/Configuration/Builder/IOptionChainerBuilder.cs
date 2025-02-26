namespace NCronJob;

/// <summary>
/// Expose a way to chain different options.
/// </summary>
public interface IOptionChainerBuilder
{
    /// <summary>
    /// Chains another option to the job.
    /// </summary>
    JobOptionBuilder And { get; }
}
