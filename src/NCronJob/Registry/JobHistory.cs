using System.Collections.Concurrent;

namespace NCronJob;

internal sealed class JobHistory
{
    //Note: This can grow out of control if not managed properly. JobHistory should ultimately be persisted to a database or something durable.
    //Everything about this model and its properties needs to be serializable.
    private readonly ConcurrentBag<JobRun> jobRuns = [];

    public void Add(JobRun jobRun) => jobRuns.Add(jobRun);

    public IReadOnlyCollection<JobRun> GetAll() => jobRuns;
}
