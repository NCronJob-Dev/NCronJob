using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// The state of a reported orchestration or job.
/// </summary>
[Experimental("NCRONJOB_OBSERVER")]
public enum ExecutionState
{
    /// <summary>
    /// Fallback state.
    /// </summary>
    Undetermined = 0,

    /// <summary>
    /// The job has been registered.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The job has been scheduled.
    /// </summary>
    Scheduled,

    /// <summary>
    /// The job is about to run.
    /// </summary>
    Initializing,

    /// <summary>
    /// The job is running.
    /// </summary>
    Running,

    /// <summary>
    /// The previous instance of the job encountered an issue. The job is retrying.
    /// </summary>
    Retrying,

    /// <summary>
    /// The job is finalizing its run.
    /// </summary>
    Completing,

    /// <summary>
    /// The job is identifying its dependent jobs to be triggered.
    /// </summary>
    WaitingForDependency,

    /// <summary>
    /// The job has been explicitly skipped by its parent job.
    /// </summary>
    Skipped,

    /// <summary>
    /// The job has completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The job has crashed.
    /// </summary>
    Faulted,

    /// <summary>
    /// The job has been cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The job expired.
    /// </summary>
    Expired,

    /// <summary>
    /// The orchestration has started.
    /// </summary>
    OrchestrationStarted,

    /// <summary>
    /// The orchestration has completed.
    /// </summary>
    OrchestrationCompleted,
}
