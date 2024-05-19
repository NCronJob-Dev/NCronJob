using System.Collections.Concurrent;

namespace LinkDotNet.NCronJob.Messaging;


internal interface IMessageMediator
{
    void RegisterHandler<TEvent>(IEventHandler<TEvent> handler);
    Task PublishAsync<T>(T message);
}
internal interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event);
}


internal sealed class MessageMediator : IMessageMediator
{
    private readonly ConcurrentDictionary<Type, List<object>> messageHandlers = new();

    public void RegisterHandler<TEvent>(IEventHandler<TEvent> handler)
    {
        var handlers = messageHandlers.GetOrAdd(typeof(TEvent), _ => new List<object>());
        handlers.Add(handler);
    }

    public async Task PublishAsync<T>(T message)
    {
        if (messageHandlers.TryGetValue(typeof(T), out var handlers))
        {
            foreach (var handler in handlers.Cast<IEventHandler<T>>())
            {
                await handler.HandleAsync(message);
            }
        }
    }
}

