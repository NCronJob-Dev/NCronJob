namespace NCronJob;

/// <summary>
/// A configuration option for a job.
/// </summary>
internal sealed class JobOption
{
    private object? parameter;

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
    public object? Parameter
    {
        get => parameter;
        set
        {
            parameter = value;
            HasParameter = true;
        }
    }

    public bool HasParameter { get; private set; }

    /// <summary>
    /// Startup Jobs will be executed once during the application startup before any other jobs.
    /// </summary>
    public bool? ShouldCrashOnStartupFailure { get; set; }

    /// <summary>
    /// The maximum execution time for this job. The default is unlimited.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Overrides how long a scheduled run may remain queued after its intended run time.
    /// </summary>
    public TimeSpan? JobRunExpiry { get; set; }

    /// <summary>
    /// The job name given by the user, which can be used to identify the job.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Conditional predicates that must all return true for the job to execute.
    /// If any condition returns false, the job will be skipped.
    /// </summary>
    public List<Func<IServiceProvider, CancellationToken, ValueTask<bool>>>? Conditions { get; set; }

    internal void SetTimeout(TimeSpan timeout)
    {
        ValidateTimeoutLikeValue(timeout, nameof(timeout));
        Timeout = timeout;
    }

    internal void SetJobRunExpiry(TimeSpan expiry)
    {
        ValidateTimeoutLikeValue(expiry, nameof(expiry));
        JobRunExpiry = expiry;
    }

    internal static void ValidateTimeoutLikeValue(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero && value != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be positive or Timeout.InfiniteTimeSpan.");
        }
    }
}
