namespace NCronJob;

internal readonly struct JobState
{
    public JobStateType Type { get; }
    public DateTimeOffset? Timestamp { get; }
    public string Message { get; }

    public JobState(JobStateType type, string message = "")
    {
        Type = type;
        Timestamp = type != JobStateType.NotStarted ? DateTimeOffset.Now : null;
        Message = message;
    }
}

internal enum JobStateType
{
    NotStarted = 0,
    Scheduled,
    Running,
    Retrying,
    Completing,
    Completed,
    Faulted,
    Cancelled,
    Expired,
    Crashed
}
