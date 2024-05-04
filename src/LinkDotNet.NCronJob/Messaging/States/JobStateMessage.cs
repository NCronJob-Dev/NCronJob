namespace LinkDotNet.NCronJob.Messaging.States;

/// <summary>
/// 
/// </summary>
public sealed class JobStateMessage
{
    public JobExecutionContext JobContext { get; set; }
    public ExecutionState State { get; set; }
    private JobRunStatus? status;

    public JobRunStatus? Status
    {
        get
        {
            if (status != null)
            {
                return status;
            }

            // Derive status from state if not explicitly set
            return State switch
            {
                ExecutionState.Completed => JobRunStatus.Completed,
                ExecutionState.Executing => JobRunStatus.InProgress,
                ExecutionState.Initializing => JobRunStatus.Enqueued,
                _ => status // Default or undefined states can explicitly set status to null or another appropriate value
            };
        }
        set => status = value;
    }

    public JobStateMessage(JobExecutionContext jobContext, ExecutionState state, JobRunStatus? status = null)
    {
        JobContext = jobContext;
        State = state;
        this.status = status;
    }
}
