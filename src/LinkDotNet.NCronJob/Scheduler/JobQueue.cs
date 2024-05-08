using System.Diagnostics.CodeAnalysis;

namespace LinkDotNet.NCronJob;

internal sealed class JobQueue : IDisposable
{
    private readonly TimeProvider timeProvider;

    private readonly PriorityQueue<JobDefinition, (DateTimeOffset NextRunTime, int Priority)> jobQueue =
        new(new JobQueueTupleComparer());

    public JobQueue(TimeProvider timeProvider) => this.timeProvider = timeProvider;

    public event EventHandler? JobEnqueued;

    public int Count => jobQueue.Count;

    public JobDefinition Dequeue() => jobQueue.Dequeue();

    public void Enqueue(JobDefinition job, (DateTimeOffset NextRunTime, int Priority) t)
        => jobQueue.Enqueue(job, t);

    public bool TryPeek([NotNullWhen(true)]out JobDefinition? jobDefinition, out (DateTimeOffset NextRunTime, int Priority) tuple)
        => jobQueue.TryPeek(out jobDefinition, out tuple);

    public void EnqueueForDirectExecution(JobDefinition job)
    {
        var utcNow = timeProvider.GetUtcNow();
        jobQueue.Enqueue(job, (utcNow, (int)job.Priority));
        JobEnqueued?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => JobEnqueued = null;
}
