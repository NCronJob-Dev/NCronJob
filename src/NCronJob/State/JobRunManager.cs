using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NCronJob;

internal class JobRunManager
{
    private readonly ConcurrentDictionary<Guid, JobRun> jobRuns = new();
    private readonly ILogger<JobRunManager> logger;

    public JobRunManager(ILogger<JobRunManager> logger) => this.logger = logger;

    public JobRun CreateJobRun(JobDefinition jobDefinition, CancellationToken token = default)
    {
        var jobRun = JobRun.Create(jobDefinition, token);
        if (jobRuns.TryAdd(jobRun.JobRunId, jobRun))
        {
            logger.LogInformation($"JobRun created: {jobRun.JobRunId}");
        }
        else
        {
            logger.LogWarning($"Failed to add JobRun: {jobRun.JobRunId}");
        }
        return jobRun;
    }

    public void RegisterJob(JobRun jobRun) => jobRuns.TryAdd(jobRun.JobRunId, jobRun);

    // create method to get all JobRuns
    public IEnumerable<JobRun> GetJobRuns() => jobRuns.Values;

    public bool TryGetJobRun(Guid jobRunId, out JobRun? jobRun) => jobRuns.TryGetValue(jobRunId, out jobRun);

    public void PauseJobRun(Guid jobRunId)
    {
        if (jobRuns.TryGetValue(jobRunId, out var jobRun))
        {
            jobRun.Pause();
            logger.LogInformation($"JobRun paused: {jobRunId}");
        }
        else
        {
            logger.LogWarning($"JobRun not found: {jobRunId}");
        }
    }

    public void ResumeJobRun(Guid jobRunId)
    {
        if (jobRuns.TryGetValue(jobRunId, out var jobRun))
        {
            jobRun.Resume();
            logger.LogInformation($"JobRun resumed: {jobRunId}");
        }
        else
        {
            logger.LogWarning($"JobRun not found: {jobRunId}");
        }
    }

    public void CancelJobRun(Guid jobRunId)
    {
        if (jobRuns.TryRemove(jobRunId, out var jobRun))
        {
            jobRun.Cancel();
            logger.LogInformation($"JobRun canceled: {jobRunId}");
        }
        else
        {
            logger.LogWarning($"JobRun not found: {jobRunId}");
        }
    }

    public void RemoveJobRun(Guid jobRunId)
    {
        if (jobRuns.TryRemove(jobRunId, out var jobRun))
        {
            logger.LogInformation($"JobRun for {jobRun.JobDefinition.JobName} removed: {jobRunId}");
        }
        else
        {
            logger.LogWarning($"JobRun not found: {jobRunId}");
        }
    }
}
