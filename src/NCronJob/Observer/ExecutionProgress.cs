using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// The snapshot of a state of the execution of a job instance.
/// </summary>
///
/// <param name="Timestamp">
/// The instant this <see cref="ExecutionProgress"/> instance was created.
/// </param>
///
/// <param name="CorrelationId">
/// The correlation identifier of an orchestration run.
/// Will decorate every reported progress of the root job and all of its dependencies.
/// </param>
///
/// <param name="State">
/// The reported state.
/// Will either relate to an orchestration, describing its start or completion or to a job belonging to an orchestration.
/// </param>
///
/// <param name="RunId">
/// The identifier of a job run within an orchestration.
/// Will be <c>null</c> when the <see cref="ExecutionProgress"/> instance relates to the start or completion of an orchestration.
/// </param>
///
/// <param name="ParentRunId">
/// The identifier of the parent job run.
/// Will be <c>null</c> when the reported instance is the root job of an orchestration,
/// or when the <see cref="ExecutionProgress"/> instance relates to the start or completion of an orchestration.
/// </param>
///
/// <param name="Name">
/// The optional custom name given to the job.
/// Will be <c>null</c> when no name was specified or when the <see cref="ExecutionProgress"/> instance relates to the start or completion of an orchestration.
/// </param>
///
/// <param name="Type">
/// The type of the job.
/// Will be <c>null</c> if the job is an anonymous function based job or when the <see cref="ExecutionProgress"/> instance relates to the start or completion of an orchestration.
/// </param>
///
/// <param name="IsTypedJob">
/// Whether the job is a class based job (implementing <see cref="IJob"/>) or not.
/// Will be <c>null</c> when the <see cref="ExecutionProgress"/> instance relates to the start or completion of an orchestration.
/// </param>
///
[Experimental("NCRONJOB_OBSERVER")]
public record ExecutionProgress(
    DateTimeOffset Timestamp,
    Guid CorrelationId,
    ExecutionState State,
    Guid? RunId,
    Guid? ParentRunId,
    string? Name,
    Type? Type,
    bool? IsTypedJob);
