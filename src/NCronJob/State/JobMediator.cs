using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NCronJob.State;

internal interface IJobMediator
{
    void RegisterJob(JobRun jobRun);
    void PauseJob(Guid jobId);
    void ResumeJob(Guid jobId);
    void CancelJob(Guid jobId);
    void JobCompleted(JobRun jobRun);
    JobRun? GetJob(Guid jobId);
}

internal class JobMediator : IJobMediator
{
    private readonly ConcurrentDictionary<Guid, JobRun> runningJobs = new();
    private readonly ILogger<JobMediator> logger;

    public JobMediator(ILogger<JobMediator> logger)
    {
        this.logger = logger;
    }

    public void RegisterJob(JobRun jobRun)
    {
        if (runningJobs.TryAdd(jobRun.JobRunId, jobRun))
        {
            logger.LogInformation($"JobRun registered: {jobRun.JobRunId}");
        }
    }

    public void PauseJob(Guid jobId)
    {
        if (runningJobs.TryGetValue(jobId, out var jobRun))
        {
            jobRun.Pause();
            logger.LogInformation($"JobRun paused: {jobId}");
        }
    }

    public void ResumeJob(Guid jobId)
    {
        if (runningJobs.TryGetValue(jobId, out var jobRun))
        {
            jobRun.Resume();
            logger.LogInformation($"JobRun resumed: {jobId}");
        }
    }

    public void CancelJob(Guid jobId)
    {
        if (runningJobs.TryRemove(jobId, out var jobRun))
        {
            jobRun.Cancel();
            logger.LogInformation($"JobRun canceled: {jobId}");
        }
    }

    public void JobCompleted(JobRun jobRun)
    {
        runningJobs.TryRemove(jobRun.JobRunId, out _);
        logger.LogInformation($"JobRun completed: {jobRun.JobRunId}");
    }

    public JobRun? GetJob(Guid jobId) => runningJobs.GetValueOrDefault(jobId);
}

