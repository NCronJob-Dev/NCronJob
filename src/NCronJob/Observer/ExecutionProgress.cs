using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// The snapshot of a state of the execution of a job instance.
/// </summary>
///
/// <param name="CorrelationId">
/// The correlation identifier of an orchestration run.
/// Will decorate every reported progress of the root job and all of its dependencies.
/// </param>
///
/// <param name="RunId">
/// The identifier of a job run within an orchestration.
/// Will be null when the <see cref="ExecutionProgress"/> relates to the start or completion of an orchestration.
/// </param>
///
/// <param name="ParentRunId">
/// The identifier of the parent job run.
/// Will be null when the reported instance is the root job of an orchestration,
/// or when the <see cref="ExecutionProgress"/> relates to the start or completion of an orchestration.
/// </param>
///
/// <param name="State">
/// The reported state.
/// Will either relate to an orchestration, describing its start or completion or to a job belonging to an orchestration.
/// </param>
///
/// <param name="Timestamp">
/// The instant this <see cref="ExecutionProgress"/> was created.
/// </param>
[Experimental("NCRONJOB_OBSERVER")]
public record ExecutionProgress(Guid CorrelationId, Guid? RunId, Guid? ParentRunId, ExecutionState State, DateTimeOffset Timestamp);
