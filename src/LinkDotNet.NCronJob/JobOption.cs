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

    /// <summary>
    /// Gets or sets the <see cref="IsolationLevel"/> of the executed job.
    /// </summary>
    public IsolationLevel IsolationLevel { get; set; }
}

/// <summary>
/// Isolation level of the job. This determines how the job is executed.
/// </summary>
public enum IsolationLevel
{
    /// <summary>
    /// The job is executed in the same thread as the scheduler. This is the default value.
    /// </summary>
    /// <remarks>
    /// Running jobs in the same thread as the scheduler can lead to performance issues if the synchronous part of the job takes long.
    /// The scheduler can't guarantee then to schedule new jobs in the given interval.
    /// </remarks>
    None,

    /// <summary>
    /// The job is executed in a new task.
    /// </summary>
    /// <remarks>
    /// This will wrap calls to the job in a new task and therefore not block the scheduler.
    /// This can decrease scalability as the number of tasks increases. Use this option with caution.
    /// Almost the same can be achieved with a <c>await Task.Yield()</c> at the beginning of the job.
    /// </remarks>
    NewTask
}
