namespace LinkDotNet.NCronJob.Messaging.States;

public enum ExecutionState
{
    /// <summary>
    /// Indicates that the job has not been started yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Indicates that the job is in the process of initializing, setting up resources or preconditions before execution.
    /// </summary>
    Initializing,

    /// <summary>
    /// Indicates that the job is currently executing.
    /// </summary>
    Executing,

    /// <summary>
    /// Indicates that the job execution is being retried following a failure.
    /// </summary>
    Retrying,

    /// <summary>
    /// Indicates that the job has completed its execution, regardless of the outcome (success or failure).
    /// </summary>
    Completed
}


public enum JobRunStatus
{
    /// <summary>
    /// Indicates that the job execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates that the job completed successfully. This status represents a final state.
    /// </summary>
    Completed,

    /// <summary>
    /// Indicates that the job is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Indicates that the job is queued for execution but has not started yet.
    /// </summary>
    Enqueued,

    /// <summary>
    /// Indicates that the job execution was cancelled, typically by a user action or system condition.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Indicates that the job was skipped, possibly due to conditions that prevent its execution.
    /// </summary>
    Skipped
}

/// <summary>
/// Represents the final state of a job run, providing a conclusive status post-execution.
/// </summary>
public enum FinalState
{
    /// <summary>
    /// Indicates that the job run has not yet completed, and no final state can be determined.
    /// </summary>
    NotComplete,

    /// <summary>
    /// Indicates that the job run completed successfully and met all its execution criteria without errors.
    /// </summary>
    Successful,

    /// <summary>
    /// Indicates that the job run failed to complete successfully. This includes scenarios where retries are enabled but the job exceeded the failure threshold.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates that the job run was cancelled before it could complete, typically due to user intervention or system shutdown.
    /// </summary>
    Cancelled
}
