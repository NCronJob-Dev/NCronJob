using Cronos;

namespace NCronJob;

/// <summary>
/// Represents a trigger that causes a job to execute.
/// </summary>
internal interface ITrigger
{
    /// <summary>
    /// Gets the type of trigger.
    /// </summary>
    TriggerType Type { get; }
}

/// <summary>
/// Represents a CRON-based trigger that executes jobs on a schedule.
/// </summary>
internal sealed record CronTrigger : ITrigger
{
    public CronTrigger(CronExpression cronExpression, string userDefinedCronExpression, TimeZoneInfo? timeZone)
    {
        CronExpression = cronExpression;
        UserDefinedCronExpression = userDefinedCronExpression;
        TimeZone = timeZone;
    }

    public TriggerType Type => TriggerType.Cron;

    public CronExpression CronExpression { get; }

    /// <summary>
    /// This is the unhandled cron expression from the user. Using <see cref="CronExpression.ToString"/> will alter the expression.
    /// </summary>
    public string UserDefinedCronExpression { get; }

    public TimeZoneInfo? TimeZone { get; }

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset utcNow)
        => CronExpression.GetNextOccurrence(utcNow, TimeZone ?? TimeZoneInfo.Utc);
}

/// <summary>
/// Represents an instant/scheduled trigger that executes a job at a specific time or after a delay.
/// </summary>
internal sealed record InstantTrigger : ITrigger
{
    public InstantTrigger(DateTimeOffset runAt)
    {
        RunAt = runAt;
    }

    public TriggerType Type => TriggerType.Instant;

    public DateTimeOffset RunAt { get; }
}

/// <summary>
/// Represents a startup trigger that executes a job when the application starts.
/// </summary>
internal sealed record StartupTrigger : ITrigger
{
    public StartupTrigger(bool shouldCrashOnFailure)
    {
        ShouldCrashOnFailure = shouldCrashOnFailure;
    }

    public TriggerType Type => TriggerType.Startup;

    public bool ShouldCrashOnFailure { get; }
}

/// <summary>
/// Represents a dependent trigger that executes a job when another job completes.
/// </summary>
internal sealed record DependentTrigger : ITrigger
{
    public TriggerType Type => TriggerType.Dependent;
}
