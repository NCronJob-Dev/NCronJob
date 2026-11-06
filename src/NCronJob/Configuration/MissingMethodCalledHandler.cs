namespace NCronJob;

internal sealed class MissingMethodCalledHandler(JobRegistry jobRegistry)
{
    public bool UseWasCalled { get; set; }

    public void EnsureUseWasCalledWhenStartupJobsExist()
    {
        if (UseWasCalled || jobRegistry.GetAllStartupJobs().Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Startup jobs have been registered. However, neither IHost.UseNCronJobAsync(), nor IHost.UseNCronJob() have been called.");
    }
}
