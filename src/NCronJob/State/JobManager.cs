using Microsoft.Extensions.Logging;

namespace NCronJob;

internal class JobManager : IJobManager
{
    private readonly JobRunManager jobRunManager;
    private readonly JobQueueManager jobQueueManager;
    private readonly ILogger<JobManager> logger;

    public JobManager(JobRunManager jobRunManager, JobQueueManager jobQueueManager, ILogger<JobManager> logger)
    {
        this.jobRunManager = jobRunManager;
        this.jobQueueManager = jobQueueManager;
        this.logger = logger;
    }

    public void PauseJob(Guid jobId)
    {
        if (jobRunManager.TryGetJobRun(jobId, out var jobRun))
        {
            ExecuteCommand(new PauseJobCommand(), jobRun!);
            logger.LogInformation($"Paused job with ID: {jobId}");
        }
    }

    public void ResumeJob(Guid jobId)
    {
        if (jobRunManager.TryGetJobRun(jobId, out var jobRun))
        {
            ExecuteCommand(new ResumeJobCommand(), jobRun!);
            logger.LogInformation($"Resumed job with ID: {jobId}");
        }
    }

    public void CancelJob(Guid jobId)
    {
        if (jobRunManager.TryGetJobRun(jobId, out var jobRun))
        {
            ExecuteCommand(new CancelJobCommand(jobRunManager), jobRun!);
            logger.LogInformation($"Canceled job with ID: {jobId}");
        }
    }

    public void PauseQueue(string queueName)
    {
        foreach (var jobRun in GetJobRunsInQueue(queueName))
        {
            ExecuteCommand(new PauseJobCommand(), jobRun);
        }
        logger.LogInformation($"Paused queue: {queueName}");
    }

    public void ResumeQueue(string queueName)
    {
        foreach (var jobRun in GetJobRunsInQueue(queueName))
        {
            ExecuteCommand(new ResumeJobCommand(), jobRun);
        }
        logger.LogInformation($"Resumed queue: {queueName}");
    }

    public void CancelQueue(string queueName)
    {
        jobQueueManager.SignalJobQueue(queueName);
        logger.LogInformation($"Canceled queue: {queueName}");
    }

    private void ExecuteCommand(IJobCommand command, JobRun jobRun) => command.Execute(jobRun);

    private IEnumerable<JobRun> GetJobRunsInQueue(string queueName) =>
        jobQueueManager.TryGetQueue(queueName, out var queue) ? [..queue] : [];
}
