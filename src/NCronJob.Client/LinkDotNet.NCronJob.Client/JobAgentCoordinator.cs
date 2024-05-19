using LinkDotNet.NCronJob.Messaging.States;
using LinkDotNet.NCronJob.Shared;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob.Client;

internal class JobAgentCoordinator : IJobAgentCoordinator
{
    public string AgentId { get; }
    private ILogger<JobAgentCoordinator> _logger;
    private readonly IJobService _jobService;

    public JobAgentCoordinator(ILogger<JobAgentCoordinator> logger,
        IJobService jobService)
    {
        _logger = logger;
        _jobService = jobService;

        AgentId = Guid.NewGuid().ToString();
    }


    public Task<List<JobDetailsView>> GetAllJobs()
    {
        _logger.LogTrace("JobAgentCoordinator.GetAllJobs() called");
        var info = _jobService.GetAllJobs();
        return Task.FromResult(info);
    }

    public Task<ExecutionState> GetJobState(Guid jobId)
    {
        _logger.LogInformation("Getting the job state");

        var jobDetails = _jobService.GetJobDetails(jobId);

        return Task.FromResult(jobDetails.CurrentState);
    }
        
    public Task<JobDetailsView> GetJobDetails(Guid jobId) =>
        Task.FromResult(_jobService.GetJobDetails(jobId));

    public Task<JobActionResult> TriggerJob(Guid jobId) =>
        ExecuteJobAction(() => _jobService.TriggerJobAsync(jobId), "Trigger");

    public Task<JobActionResult> CancelJob(Guid jobId) =>
        ExecuteJobAction(() => _jobService.CancelRunningJobAsync(jobId), "Cancel");

    private async Task<JobActionResult> ExecuteJobAction(Func<Task<(bool Success, string Message)>> jobAction, string actionName)
    {
        try
        {
            _logger.LogInformation("{ActionName} job initiated.", actionName);
            var (success, message) = await jobAction();
            var level = success ? JobActionResultLevel.Info : JobActionResultLevel.Warning;
            return new JobActionResult(level, message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{ActionName} job failed.", actionName);
            return new JobActionResult (JobActionResultLevel.Error, e.Message);
        }
    }
}
