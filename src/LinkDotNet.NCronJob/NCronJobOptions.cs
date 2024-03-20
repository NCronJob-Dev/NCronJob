namespace LinkDotNet.NCronJob;

/// <summary>
/// A configuration for the NCronJob-Scheduler.
/// </summary>
public sealed class NCronJobOptions
{
    /// <summary>
    /// The interval in which the timer schedules and executes new jobs. The default value is one second.
    /// </summary>
    /// <remarks>
    /// The higher the value, the longer it will take to run instant jobs.
    /// </remarks>
    public TimeSpan TimerInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Determines whether cron expressions can specify second-level precision.
    /// </summary>
    /// <remarks>
    /// When enabled, cron expressions must include a seconds field, allowing for more precise scheduling. 
    /// By default, this is disabled, and cron expressions are expected to start with the minute field. 
    /// Enabling this affects scheduling granularity and may influence performance, especially for jobs 
    /// that are scheduled to run very frequently.
    /// </remarks>
    public bool EnableSecondPrecision { get; set; }
}
