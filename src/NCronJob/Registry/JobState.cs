using System.Diagnostics;

namespace NCronJob;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal readonly struct JobState
{
    public JobStateType Type { get; }
    public DateTimeOffset Timestamp { get; }
    public string? Message { get; }

    public JobState(JobStateType type, string? message = default)
    {
        Type = type;
        Timestamp = DateTimeOffset.Now;
        Message = message;
    }

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
    Completed,
    Faulted,
    Cancelled,
    Expired,
    Crashed
}
