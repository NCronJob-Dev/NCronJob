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
}

internal static class JobStateExtensions
{
    public static bool IsUnchangedAndNotRetrying(this JobState current, JobStateType nextState)
        => current.Type == nextState && nextState != JobStateType.Retrying;

    public static bool IsFinalState(this JobState current) =>
       current.Type is
       JobStateType.Skipped or
       JobStateType.Completed or
       JobStateType.Cancelled or
       JobStateType.Faulted or
       JobStateType.Expired;

    public static bool CanInitiateRun(this JobState current) =>
        current.Type is
        JobStateType.Initializing or
        JobStateType.Retrying;

    public static bool CanBeCancelled(this JobState current) =>
        current.Type is
        JobStateType.NotStarted or
        JobStateType.Scheduled ||
        current.CanInitiateRun();
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
