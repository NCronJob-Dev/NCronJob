using System.Collections.Concurrent;

namespace LinkDotNet.NCronJob.Messaging;


internal sealed class SimpleMessageHub : IMessageHub
{
    private readonly ConcurrentDictionary<Guid, (Type type, Action<object> action)> subscriptions = new();

    public Guid Subscribe<T>(Action<T> action)
    {
        var subscriptionId = Guid.NewGuid();
        var wrappedAction = new Action<object>(o => action((T)o));
        subscriptions.TryAdd(subscriptionId, (typeof(T), wrappedAction));
        return subscriptionId;
    }

    public void Unsubscribe(Guid subscriptionId)
    {
        subscriptions.TryRemove(subscriptionId, out var _);
    }

    public async Task PublishAsync<T>(T message)
    {
        var messageType = typeof(T);
        List<Exception> exceptions = null;

        foreach (var (type, action) in subscriptions.Values)
        {
            if (type == messageType)
            {
                try
                {
                    await Task.Run(() => action(message));
                }
                catch (Exception e)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(e);
                }
            }
        }

        if (exceptions != null)
            throw new AggregateException("Errors occurred during message publication.", exceptions);
    }
}
