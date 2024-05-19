using LinkDotNet.NCronJob.Messaging;
using LinkDotNet.NCronJob.Messaging.States;

namespace NCronJobSample;


// This is testing the event bus notifications
public class StateChangeObserver2
{
    private readonly IMessageHub messageHub;

    public StateChangeObserver2(IMessageHub messageHub)
    {
        this.messageHub = messageHub;
        SubscribeToStateChanges();
    }

    private void SubscribeToStateChanges()
    {
        Guid subscriptionId = messageHub.Subscribe<JobStateMessage>(HandleStateChange);
    }

    private void HandleStateChange(JobStateMessage message)
    {
        Console.WriteLine($"%%%%%%%%%%%%  The Event Bus Received state change for job {message.JobContext.Id}: {message.State} with status {message.Status}");
    }
}
