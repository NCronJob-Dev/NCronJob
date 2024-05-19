using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LinkDotNet.NCronJob.Messaging;

public enum JobLifecycleEvent
{
    /// <summary>
    /// Emitted when a job is scheduled into the system.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Emitted when a job starts its execution.
    /// </summary>
    Started,

    /// <summary>
    /// Emitted when a job execution is paused, if supported.
    /// </summary>
    Paused,

    /// <summary>
    /// Emitted when a job execution resumes from a paused state.
    /// </summary>
    Resumed,

    /// <summary>
    /// Emitted when a job completes its execution successfully.
    /// </summary>
    CompletedSuccessfully,

    /// <summary>
    /// Emitted when a job fails to complete successfully.
    /// </summary>
    Failed,

    /// <summary>
    /// Emitted when a job is cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Emitted when a job is terminated or killed, typically during system shutdown or due to an external command.
    /// </summary>
    Terminated
}

