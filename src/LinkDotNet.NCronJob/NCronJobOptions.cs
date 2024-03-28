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
    /// The higher the value, the longer it will take to run (instant) jobs.
    /// On every tick, the scheduler looks what has to run from now to the next interval.
    /// So if the interval is large, it can take longer for a job to start.
    /// </remarks>
    public TimeSpan TimerInterval { get; set; } = TimeSpan.FromSeconds(1);
}
