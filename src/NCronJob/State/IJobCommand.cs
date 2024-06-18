
namespace NCronJob;

internal interface IJobCommand
{
    void Execute(JobRun jobRun);
}

internal class PauseJobCommand : IJobCommand
{
    public void Execute(JobRun jobRun)
    {
        jobRun.Pause();
    }
}

internal class ResumeJobCommand : IJobCommand
{
    public void Execute(JobRun jobRun)
    {
        jobRun.Resume();
    }
}

internal class CancelJobCommand : IJobCommand
{
    private readonly JobRunManager jobRunManager;

    public CancelJobCommand(JobRunManager jobRunManager)
    {
        this.jobRunManager = jobRunManager;
    }

    public void Execute(JobRun jobRun)
    {
        jobRunManager.CancelJobRun(jobRun.JobRunId);
    }
}
