using LinkDotNet.NCronJob.Messaging.States;

namespace NCronJobSample;


// This is a test for the StateChangedAsync event in JobStateManager
public class StateChangeObserver
{
    private readonly JobStateManager jobStateManager;

    public StateChangeObserver(JobStateManager jobStateManager)
    {
        this.jobStateManager = jobStateManager;
        this.jobStateManager.StateChangedAsync += HandleStateChangeAsync;
    }

    private async Task HandleStateChangeAsync(JobStateMessage message)
    {
        Console.WriteLine($"%%%%  The StateChangedAsync Received state change for job {message.JobContext.Id}: {message.State} with status {message.Status}");
    }
}
