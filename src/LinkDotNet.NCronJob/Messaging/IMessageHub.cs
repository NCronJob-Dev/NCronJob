namespace LinkDotNet.NCronJob.Messaging;

public interface IMessageHub
{
    Guid Subscribe<T>(Action<T> action);
    void Unsubscribe(Guid subscriptionId);
    Task PublishAsync<T>(T message);
}
