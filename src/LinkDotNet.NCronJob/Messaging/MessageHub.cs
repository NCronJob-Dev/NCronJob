using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LinkDotNet.NCronJob.Messaging;

internal sealed class MessageHub : IMessageHub
{
    private readonly ConcurrentDictionary<Type, Channel<object>> channels = new();
    private readonly ConcurrentDictionary<Guid, Subscription> subscriptions = new();

    public Guid Subscribe<T>(Action<T> action)
    {
        var type = typeof(T);
        var channel = channels.GetOrAdd(type, _ => Channel.CreateUnbounded<object>());

        var cancellationTokenSource = new CancellationTokenSource();
        var subscriptionId = Guid.NewGuid();

        var processingTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync(cancellationTokenSource.Token))
                {
                    action((T)item);
                }
            }
            catch (OperationCanceledException)
            {
                // Log cancellation or handle gracefully
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }, cancellationTokenSource.Token);

        subscriptions[subscriptionId] = new Subscription(type, processingTask, cancellationTokenSource);
        return subscriptionId;
    }

    public void Unsubscribe(Guid subscriptionId)
    {
        if (subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            subscription.CancellationTokenSource.Cancel();
            subscription.ProcessingTask.ContinueWith(t =>
            {
                CloseChannelWhenNoSubscribers(subscription);
                subscription.CancellationTokenSource.Dispose();
            }, TaskScheduler.Default);
        }
    }

    private void CloseChannelWhenNoSubscribers(Subscription subscription)
    {
        // Close the channel if no more subscribers are present
        if (subscriptions.Values.All(s => s.Type != subscription.Type))
        {
            if (channels.TryRemove(subscription.Type, out var channel))
            {
                channel.Writer.Complete();
            }
        }
    }

    public async Task PublishAsync<T>(T message)
    {
        if (channels.TryGetValue(typeof(T), out var channel))
        {
            await channel.Writer.WriteAsync(message);
        }
    }

    private class Subscription(Type type, Task processingTask, CancellationTokenSource cancellationTokenSource)
    {
        public Type Type { get; } = type;
        public Task ProcessingTask { get; } = processingTask;
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;
    }
}

