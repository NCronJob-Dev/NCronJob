namespace NCronJob;

internal class JobState
{
    public JobStateType Type { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Message { get; private set; }

    public JobState(JobStateType type, string message = "")
    {
        Type = type;
        Timestamp = DateTime.UtcNow;
        Message = message;
    }
}

internal enum JobStateType
{
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled,
    Expired,
    Crashed
}
