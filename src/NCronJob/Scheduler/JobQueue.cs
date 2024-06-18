namespace NCronJob;

/// <summary>
/// Represents the internal work queue. This represents all scheduled and running CRON jobs as well as instant jobs.
/// </summary>
internal sealed class JobQueue : ObservablePriorityQueue<JobRun>
{
    private readonly TimeProvider timeProvider;

    public string Name { get; set; }

    public JobQueue(TimeProvider timeProvider, string name) : base(new JobQueueTupleComparer())
    {
        this.timeProvider = timeProvider;
        Name = name;
    }

    /// <summary>
    /// Adds a job entry to this instance.
    /// </summary>
    /// <param name="job">The job that will be added.</param>
    /// <param name="when">An optional <see cref="DateTimeOffset"/> object representing when the job should run. If <c>null</c> then it'll
    /// fall back to the job.RunAt. If the job.RunAt is not defined then it runs immediately.</param>
    public void EnqueueForDirectExecution(JobRun job, DateTimeOffset? when = null)
    {
        when ??= job.RunAt ?? timeProvider.GetUtcNow();
        Enqueue(job, (when.Value, (int)job.Priority));
    }

    public void RemoveByName(string jobName) => RemoveByPredicate(t => t.JobDefinition.CustomName == jobName);
}
