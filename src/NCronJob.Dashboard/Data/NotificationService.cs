using LinkDotNet.NCronJob.Shared;

namespace NCronJob.Dashboard.Data;

internal class NotificationService(EventAggregator eventAggregator)
{
    public void NotifyJobDetailsReceivedForAgent(JobDetailsView jobDetailsView, string agentId)
    {
        var eventData = new JobDetailsReceivedEvent(jobDetailsView, agentId);
        eventAggregator.Publish(eventData);
    }

    public void Subscribe<TEventData>(Action<TEventData> handler) where TEventData : IJobReceiveEvent
    {
        eventAggregator.Subscribe(handler);
    }

    public void Unsubscribe<TEventData>(Action<TEventData> handler) where TEventData: IJobReceiveEvent
    {
        eventAggregator.Unsubscribe(handler);
    }
}

public interface IJobReceiveEvent
{
    public string AgentId { get; }
}

internal record JobDetailsReceivedEvent(JobDetailsView JobDetails, string AgentId) : IJobReceiveEvent;
