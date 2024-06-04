using System.Collections.Concurrent;

namespace NCronJob;

internal sealed class JobHistory
{
    private readonly ConcurrentBag<JobRun> jobRuns = [];

    public void Add(JobRun jobRun) => jobRuns.Add(jobRun);

    public IReadOnlyCollection<JobRun> GetAll() => jobRuns;
}
