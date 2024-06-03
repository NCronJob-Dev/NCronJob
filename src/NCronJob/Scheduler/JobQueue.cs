using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// Represents the internal work queue. This represents all scheduled and running CRON jobs as well as instant jobs.
/// </summary>
internal sealed class JobQueue : IDisposable
{
    private readonly TimeProvider timeProvider;

    private readonly PriorityQueue<JobRun, (DateTimeOffset NextRunTime, int Priority)> jobQueue =
        new(new JobQueueTupleComparer());

    public JobQueue(TimeProvider timeProvider) => this.timeProvider = timeProvider;

    /// <summary>
    /// This will be triggered when the job queue has changes and therefore upcoming runs need reevaluation.
    /// </summary>
    public event EventHandler? JobQueueChanged;

    public int Count => jobQueue.Count;

    public JobRun Dequeue() => jobQueue.Dequeue();

    public void Enqueue(JobRun job, (DateTimeOffset NextRunTime, int Priority) tuple)
        => jobQueue.Enqueue(job, tuple);

    public bool TryPeek([NotNullWhen(true)]out JobRun? jobDefinition, out (DateTimeOffset NextRunTime, int Priority) tuple)
        => jobQueue.TryPeek(out jobDefinition, out tuple);

    /// <summary>
    /// Adds an entry to this instance and triggers the <see cref="JobQueueChanged"/> event.
    /// </summary>
    /// <param name="job">The job that will be added.</param>
    /// <param name="when">An optional <see cref="DateTimeOffset"/> object representing when the job should run. If <code>null</code> it will run immediately.</param>
    public void EnqueueForDirectExecution(JobRun job, DateTimeOffset? when = null)
    {
        when ??= timeProvider.GetUtcNow();
        jobQueue.Enqueue(job, (when.Value, (int)job.Priority));
        JobQueueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Signals that the internal job queue has changes and a new evaluation is needed.
    /// </summary>
    public void ReevaluateQueue() => JobQueueChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => JobQueueChanged = null;

    /// <summary>
    /// Removes all CRON jobs from the queue.
    /// </summary>
    public void RemoveAllCron()
    {
        var nonCronJobs = jobQueue.UnorderedItems.Where(s => s.Element.JobDefinition.CronExpression is null).ToArray();
        jobQueue.Clear();
        jobQueue.EnqueueRange(nonCronJobs);
    }
}
