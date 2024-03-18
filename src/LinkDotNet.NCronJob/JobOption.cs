namespace LinkDotNet.NCronJob;

/// <summary>
/// A configuration option for a job.
/// </summary>
public sealed class JobOption
{
    /// <summary>
    /// Set's the cron expression for the job. If set to null, the job is added to the container but will not be scheduled.
    /// </summary>
    /// <remarks>
    /// The <see cref="IInstantJobRegistry"/> retrieves the job instance from the container, therefore instant jobs have to
    /// be registered as well. They dont need the <see cref="CronExpression"/> set to something.
    /// </remarks>
    public string? CronExpression { get; set; }

    /// <summary>
    /// The parameter that can be passed down to the job. This only applies to cron jobs.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    public object? Parameter { get; set; }
}
