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

    private string DebuggerDisplay => $"Type = {Type}, Timestamp = {Timestamp}";

    public static implicit operator JobStateType(JobState jobState) => jobState.Type;
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
