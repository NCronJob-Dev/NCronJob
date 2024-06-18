namespace NCronJob;

internal interface IJobManager
{
    void PauseJob(Guid jobId);
    void ResumeJob(Guid jobId);
    void CancelJob(Guid jobId);
    void PauseQueue(string queueName);
    void ResumeQueue(string queueName);
    void CancelQueue(string queueName);
}
