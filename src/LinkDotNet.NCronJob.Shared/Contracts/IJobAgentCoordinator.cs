using LinkDotNet.NCronJob.Messaging.States;

namespace LinkDotNet.NCronJob.Shared;

public interface IJobAgentCoordinator
{
    Task<List<JobDetailsView>> GetAllJobs();
    Task<ExecutionState> GetJobState(Guid jobId);
    Task<JobDetailsView> GetJobDetails(Guid jobId);
    Task<JobActionResult> TriggerJob(Guid jobId);
    Task<JobActionResult> CancelJob(Guid jobId);
}

public record JobActionResult(JobActionResultLevel Level, string? Message)
{
    public override string? ToString()
    {
            return Message;
        }
}
public enum JobActionResultLevel
{
    Info,
    Success,
    Warning,
    Error
}
