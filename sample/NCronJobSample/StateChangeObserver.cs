using LinkDotNet.NCronJob.Messaging.States;

namespace NCronJobSample;


// This is a test for the StateChangedAsync event in JobStateManager
internal class StateChangeObserver
{
    private readonly IMonitorStateCronJobs jobStateManager;

    public StateChangeObserver(IMonitorStateCronJobs jobStateManager)
    {
        this.jobStateManager = jobStateManager;
        this.jobStateManager.JobStatusChanged += HandleStateChangeAsync;
    }

    private async Task HandleStateChangeAsync(JobStateMessage message)
    {
        Console.WriteLine($"%%%%  The StateChangedAsync Received state change for job {message.JobContext.Id}: {message.State} with status {message.Status}");
    }
}
