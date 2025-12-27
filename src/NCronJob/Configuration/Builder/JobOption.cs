namespace NCronJob;

/// <summary>
/// A configuration option for a job.
/// </summary>
internal class JobOption
{
    /// <summary>
    /// Set's the cron expression for the job. If set to null, the job is added to the container but will not be scheduled.
    /// </summary>
    /// <remarks>
    /// The <see cref="IInstantJobRegistry"/> retrieves the job instance from the container, therefore instant jobs have to
    /// be registered as well. They don't need the <see cref="CronExpression"/> set to something.
    /// </remarks>
    public string? CronExpression { get; set; }

    /// <summary>
    /// The timezone that is used to evaluate the cron expression. Defaults to UTC.
    /// </summary>
    public TimeZoneInfo? TimeZoneInfo { get; set; }

    /// <summary>
    /// The parameter that can be passed down to the job. This only applies to cron jobs.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    public object? Parameter { get; set; }

    /// <summary>
    /// Startup Jobs will be executed once during the application startup before any other jobs.
    /// </summary>
    public bool? ShouldCrashOnStartupFailure { get; set; }

    /// <summary>
    /// The job name given by the user, which can be used to identify the job.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Conditional predicates that must all return true for the job to execute.
    /// If any condition returns false, the job will be skipped.
    /// </summary>
    public List<Func<IServiceProvider, CancellationToken, ValueTask<bool>>>? Conditions { get; set; }
}
