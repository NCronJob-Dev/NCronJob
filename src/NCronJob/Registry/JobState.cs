using System.Diagnostics;

namespace NCronJob;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal readonly struct JobState
{
    public JobStateType Type { get; }
    public DateTimeOffset Timestamp { get; }
    public Exception? Fault { get; }

    public JobState(
        JobStateType type,
        DateTimeOffset utcNow,
        Exception? fault = default)
    {
        Debug.Assert(fault is not null || type != JobStateType.Faulted);

        Type = type;
        Timestamp = utcNow;
        Fault = fault;
    }

    public bool IsUnchangedAndNotRetrying(JobStateType nextState)
        => Type == nextState && nextState != JobStateType.Retrying;

    public bool IsFinalState() =>
       Type is
       JobStateType.Skipped or
       JobStateType.Completed or
       JobStateType.Cancelled or
       JobStateType.Faulted or
       JobStateType.Expired;

    public bool CanInitiateRun() =>
        Type is
        JobStateType.Initializing or
        JobStateType.Retrying;

    public bool CanBeCancelled() =>
        Type is JobStateType.NotStarted or JobStateType.Scheduled
        || CanInitiateRun();

    private string DebuggerDisplay => $"Type = {Type}, Timestamp = {Timestamp}";
}

internal enum JobStateType
{
    NotStarted = 0,
    Scheduled,
    Initializing,
    Running,
    Retrying,
    Completing,
    WaitingForDependency,
    Skipped,
    Completed,
    Faulted,
    Cancelled,
    Expired,
}
