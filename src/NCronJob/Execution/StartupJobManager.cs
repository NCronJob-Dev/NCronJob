namespace NCronJob;

internal class StartupJobManager(
    JobRegistry jobRegistry,
    JobProcessor jobProcessor,
    JobExecutionProgressObserver observer)
{
    public async Task ProcessStartupJobs(CancellationToken stopToken)
    {
        var startupJobs = jobRegistry.GetAllOneTimeJobs();
        var startupTasks = startupJobs.Select(definition => CreateExecutionTask(JobRun.Create(observer.Report, definition), stopToken)).ToList();

        if (startupTasks.Count > 0)
        {
            await Task.WhenAll(startupTasks).ConfigureAwait(false);
        }
    }

    private async Task CreateExecutionTask(JobRun job, CancellationToken stopToken) =>
        await jobProcessor.ProcessJobAsync(job, stopToken).ConfigureAwait(false);
}
