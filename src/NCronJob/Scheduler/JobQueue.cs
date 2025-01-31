namespace NCronJob;

/// <summary>
/// Represents the internal work queue. This represents all scheduled and running CRON jobs as well as instant jobs.
/// </summary>
internal sealed class JobQueue : ObservablePriorityQueue<JobRun>
{
    public string Name { get; set; }

    public JobQueue(string name) : base(new JobQueueTupleComparer())
    {
        Name = name;
    }

    /// <summary>
    /// Adds a job entry to this instance.
    /// </summary>
    /// <param name="job">The job that will be added.</param>
    public void EnqueueForDirectExecution(JobRun job)
    {
        Enqueue(job, (job.RunAt, (int)job.Priority));
    }
}
