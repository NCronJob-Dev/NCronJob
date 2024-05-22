namespace NCronJob;
internal class StartupJobManager(JobRegistry jobRegistry)
{
    private readonly AsyncManualResetEvent startupJobsCompleted = new();

    public async Task ProcessStartupJobs(Func<JobDefinition, CancellationToken, Task> executeJob, CancellationToken stopToken)
    {
        var startupJobs = jobRegistry.GetAllOneTimeJobs();

        var startupTasks = startupJobs.Select(job => executeJob(job, stopToken)).ToList();

        if (startupTasks.Any())
        {
            await Task.WhenAll(startupTasks);
        }

        startupJobsCompleted.Set();
    }

    public Task WaitForStartupJobsCompletion() => startupJobsCompleted.WaitAsync();
}

internal class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync() => tcs.Task;

    public void Set() => tcs.TrySetResult(true);

    public void Reset() => tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

