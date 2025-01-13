using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// The snapshot of a state of the execution of a job instance.
/// </summary>
[Experimental("NCRONJOB_OBSERVER")]
public record ExecutionProgress
{
    internal ExecutionProgress(JobRun run)
    {
        RunId = run.JobRunId;
        ParentRunId = run.ParentJobRunId;
        CorrelationId = run.CorrelationId;
        State = MapFrom(run.CurrentState.Type);
    }

    /// <summary>
    /// The identifier of a job run within an orchestration.
    /// </summary>
    /// <remarks>Will be null when the <see cref="ExecutionProgress"/> relates to the start or completion of an orchestration.</remarks>
    public Guid? RunId { get; init; }

    /// <summary>
    /// The identifier of the parent job run.
    /// </summary>
    /// <remarks>Will be null when the reported instance is the root job of an orchestration,
    /// or when the <see cref="ExecutionProgress"/> relates to the start or completion of an orchestration.</remarks>
    public Guid? ParentRunId { get; init; }

    /// <summary>
    /// The correlation identifier of an orchestration run. Will decorate every reported progress of the root job and all of its dependencies.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// The reported state. Will either relate to an orchestration, describing its start or completion or to a job belonging to an orchestration.
    /// </summary>
    public ExecutionState State { get; init; }

    /// <summary>
    /// The instant this <see cref="ExecutionProgress"/> was created./>
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;

    private static ExecutionState MapFrom(JobStateType currentState)
    {
        return currentState switch
        {
            JobStateType.NotStarted => ExecutionState.NotStarted,
            JobStateType.Scheduled => ExecutionState.Scheduled,
            JobStateType.Initializing => ExecutionState.Initializing,
            JobStateType.Running => ExecutionState.Running,
            JobStateType.Retrying => ExecutionState.Retrying,
            JobStateType.Completing => ExecutionState.Completing,
            JobStateType.WaitingForDependency => ExecutionState.WaitingForDependency,
            JobStateType.Completed => ExecutionState.Completed,
            JobStateType.Faulted => ExecutionState.Faulted,
            JobStateType.Cancelled => ExecutionState.Cancelled,
            JobStateType.Expired => ExecutionState.Expired,
            _ => ExecutionState.Undetermined,
        };
    }
}
