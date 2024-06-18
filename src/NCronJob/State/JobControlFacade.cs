namespace NCronJob;

public interface IJobsController
{
    void PauseJob(Guid jobId);
    void ResumeJob(Guid jobId);
    void CancelJob(Guid jobId);
    void PauseQueue(string queueName);
    void ResumeQueue(string queueName);
    void CancelQueue(string queueName);
}

/// <summary>
/// 
/// </summary>
internal class JobControlFacade : IJobsController
{
    private readonly IJobManager jobManager;

    public JobControlFacade(IJobManager jobManager)
    {
        this.jobManager = jobManager;
    }

    public void PauseJob(Guid jobId)
    {
        jobManager.PauseJob(jobId);
    }

    public void ResumeJob(Guid jobId)
    {
        jobManager.ResumeJob(jobId);
    }

    public void CancelJob(Guid jobId)
    {
        jobManager.CancelJob(jobId);
    }

    public void PauseQueue(string queueName)
    {
        jobManager.PauseQueue(queueName);
    }

    public void ResumeQueue(string queueName)
    {
        jobManager.ResumeQueue(queueName);
    }

    public void CancelQueue(string queueName)
    {
        jobManager.CancelQueue(queueName);
    }
}
