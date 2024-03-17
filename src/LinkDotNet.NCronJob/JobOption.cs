namespace LinkDotNet.NCronJob;

/// <summary>
/// A configuration option for a job.
/// </summary>
public sealed class JobOption
{
    /// <summary>
    /// Set's the cron expression for the job. If set to null, the job is added to the container but will not be scheduled
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// The parameter that can be passed down to the job. This only applies to cron jobs.
    /// </summary>
    public object? Parameter { get; set; }
}
