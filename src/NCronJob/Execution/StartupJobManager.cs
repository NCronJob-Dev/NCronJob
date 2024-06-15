namespace NCronJob;
internal class StartupJobManager(JobRegistry jobRegistry, JobProcessor jobProcessor)
{
    private readonly AsyncManualResetEvent startupJobsCompleted = new();

    public async Task ProcessStartupJobs(CancellationToken stopToken)
    {
        var startupJobs = jobRegistry.GetAllOneTimeJobs();
        var startupTasks = startupJobs.Select(definition => CreateExecutionTask(JobRun.Create(definition), stopToken)).ToList();

        if (startupTasks.Count > 0)
        {
            await Task.WhenAll(startupTasks).ConfigureAwait(false);
        }

        startupJobsCompleted.Set();
    }

    private async Task CreateExecutionTask(JobRun job, CancellationToken stopToken) =>
        await jobProcessor.ProcessJobAsync(job, stopToken).ConfigureAwait(false);

    public Task WaitForStartupJobsCompletion() => startupJobsCompleted.WaitAsync();
}

internal class AsyncManualResetEvent
{
    private TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync() => tcs.Task;

    public void Set() => tcs.TrySetResult();

    public void Reset() => tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

