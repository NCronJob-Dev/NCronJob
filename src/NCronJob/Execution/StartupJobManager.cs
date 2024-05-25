namespace NCronJob;
internal class StartupJobManager(JobRegistry jobRegistry)
{
    private readonly AsyncManualResetEvent startupJobsCompleted = new();

    public async Task ProcessStartupJobs(Func<JobDefinition, CancellationToken, Task> executeJob, CancellationToken stopToken)
    {
        var startupJobs = jobRegistry.GetAllOneTimeJobs();

        var startupTasks = startupJobs.Select(job => executeJob(job, stopToken)).ToList();

        if (startupTasks.Count > 0)
        {
            await Task.WhenAll(startupTasks);
        }

        startupJobsCompleted.Set();
    }

    public Task WaitForStartupJobsCompletion() => startupJobsCompleted.WaitAsync();
}

internal class AsyncManualResetEvent
{
    private TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync() => tcs.Task;

    public void Set() => tcs.TrySetResult();

    public void Reset() => tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

