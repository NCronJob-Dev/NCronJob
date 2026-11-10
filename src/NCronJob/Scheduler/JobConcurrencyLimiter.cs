namespace NCronJob;

internal sealed class JobConcurrencyLimiter
{
    private readonly Dictionary<string, int> runningJobCounts = [];
    private int totalRunningJobCount;
    private readonly AsyncSignal capacitySignal = new();
    private readonly Lock slotLock = new();
    private readonly ConcurrencySettings concurrencySettings;

    public JobConcurrencyLimiter(ConcurrencySettings concurrencySettings)
    {
        this.concurrencySettings = concurrencySettings;
    }

    public bool TryAcquire(JobDefinition jobDefinition)
    {
        lock (slotLock)
        {
            runningJobCounts.TryGetValue(jobDefinition.JobFullName, out var currentCount);

            if (currentCount >= MaxAllowed(jobDefinition) || totalRunningJobCount >= concurrencySettings.MaxDegreeOfParallelism)
            {
                return false;
            }

            IncrementUnsafe(jobDefinition.JobFullName, currentCount);
            return true;
        }
    }

    public bool IsJobAtCapacity(JobDefinition jobDefinition)
    {
        lock (slotLock)
        {
            runningJobCounts.TryGetValue(jobDefinition.JobFullName, out var currentCount);
            return currentCount >= MaxAllowed(jobDefinition);
        }
    }

    private static int MaxAllowed(JobDefinition jobDefinition) =>
        jobDefinition.ConcurrencyPolicy?.MaxDegreeOfParallelism ?? 1;

    public void AcquireIgnoringLimits(JobDefinition jobDefinition)
    {
        lock (slotLock)
        {
            runningJobCounts.TryGetValue(jobDefinition.JobFullName, out var currentCount);
            IncrementUnsafe(jobDefinition.JobFullName, currentCount);
        }
    }

    public void Release(JobDefinition jobDefinition)
    {
        lock (slotLock)
        {
            runningJobCounts.TryGetValue(jobDefinition.JobFullName, out var currentCount);
            runningJobCounts[jobDefinition.JobFullName] = Math.Max(0, currentCount - 1);
            totalRunningJobCount = Math.Max(0, totalRunningJobCount - 1);

            capacitySignal.Pulse();
        }
    }

    public Task WaitForReleaseAsync()
    {
        lock (slotLock)
        {
            return capacitySignal.WaitAsync();
        }
    }

    private void IncrementUnsafe(string jobFullName, int currentCount)
    {
        runningJobCounts[jobFullName] = currentCount + 1;
        totalRunningJobCount++;
    }
}
