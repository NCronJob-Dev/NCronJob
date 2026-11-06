namespace NCronJob;

/// <summary>
/// Turns the finished run of a job into runs of its dependent jobs.
/// </summary>
internal sealed class DependentJobDispatcher
{
    private readonly JobRegistry jobRegistry;
    private readonly JobQueueManager jobQueueManager;

    public DependentJobDispatcher(JobRegistry jobRegistry, JobQueueManager jobQueueManager)
    {
        this.jobRegistry = jobRegistry;
        this.jobQueueManager = jobQueueManager;
    }

    public void Dispatch(JobExecutionContext context, bool success)
    {
        var jobRun = context.JobRun;
        var dependencies = success
            ? jobRegistry.GetDependentSuccessJobs(jobRun.JobDefinition)
            : jobRegistry.GetDependentFaultedJobs(jobRun.JobDefinition);

        if (dependencies.Count > 0)
            jobRun.NotifyStateChange(JobStateType.WaitingForDependency);

        foreach (var dependentJob in dependencies)
        {
            var newRun = jobRun.CreateDependent(
                dependentJob,
                dependentJob.Parameter,
                jobRun.CancellationToken);

            newRun.ParentOutput = context.Output;

            if (!context.ExecuteChildren)
            {
                newRun.NotifyStateChange(JobStateType.Skipped);
                continue;
            }

            jobQueueManager.Enqueue(newRun);
        }
    }
}
