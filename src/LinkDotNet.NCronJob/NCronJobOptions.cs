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
}
