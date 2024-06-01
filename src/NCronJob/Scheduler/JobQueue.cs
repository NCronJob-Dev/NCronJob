
using System.Collections.Concurrent;

namespace NCronJob;

/// <summary>
/// Represents the internal work queue. This represents all scheduled and running CRON jobs as well as instant jobs.
/// </summary>
internal sealed class JobQueue : ObservablePriorityQueue<JobDefinition>
{
    private readonly TimeProvider timeProvider;

    public JobQueue(TimeProvider timeProvider) : base(new JobQueueTupleComparer()) =>
        this.timeProvider = timeProvider;
    
    /// <summary>
    /// Adds a job entry to this instance.
    /// </summary>
    /// <param name="job">The job that will be added.</param>
    /// <param name="when">An optional <see cref="DateTimeOffset"/> object representing when the job should run. If <code>null</code> it will run immediately.</param>
    public void EnqueueForDirectExecution(JobDefinition job, DateTimeOffset? when = null)
    {
        when ??= timeProvider.GetUtcNow();
        Enqueue(job, (when.Value, (int)job.Priority));
    }
}

internal sealed class JobQueueSet : ConcurrentDictionary<string, JobQueue>
{ }
