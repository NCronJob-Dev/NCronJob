namespace LinkDotNet.NCronJob.Messaging.States;
public enum ExecutionState
{
    /// <summary>
    /// Indicates the execution has not started.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Indicates the execution is initializing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Indicates the execution is running.
    /// </summary>
    Executing,

    /// <summary>
    /// Indicates the execution is retrying.
    /// </summary>
    Retrying,

    /// <summary>
    /// Indicates the execution has completed.
    /// </summary>
    Completed
}

public enum JobRunStatus
{
    Failed,
    Completed, // this status represents a FinalState
    InProgress,
    Enqueued,
    Cancelled,
    Skipped
}

public enum FinalState
{
    /// <summary>
    /// Indicates the run has not completed, so there is no <see cref="FinalState"/> yet.
    /// </summary>
    NotComplete,

    /// <summary>
    /// Indicates the run completed successfully.
    /// </summary>
    Successful,

    /// <summary>
    /// Indicates the run exceeded the item failure threshold and stopped running, terminating in a failed state.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates the run was cancelled by a user and stopped running.
    /// </summary>
    Cancelled
}
