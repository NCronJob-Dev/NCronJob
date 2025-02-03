using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class NotificationHandlerTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldCallNotificationHandlerWhenJobIsDone()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<SimpleJobHandler>()
        );

        await AssertSimpleJobWasProcessedAndNotified();
    }

    [Fact]
    public async Task ShouldPassDownExceptionToNotificationHandler()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<ExceptionJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<ExceptionHandler>()
        );

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Assert.Equal("InvalidOperationException", Storage.Entries[0]);
        Assert.Single(Storage.Entries);

        var filteredEvents = events.Where((e) => e.CorrelationId == orchestrationId).ToList();

        Assert.Equal(ExecutionState.OrchestrationStarted, filteredEvents[0].State);
        Assert.Equal(ExecutionState.NotStarted, filteredEvents[1].State);
        Assert.Equal(ExecutionState.Scheduled, filteredEvents[2].State);
        Assert.Equal(ExecutionState.Initializing, filteredEvents[3].State);
        Assert.Equal(ExecutionState.Running, filteredEvents[4].State);
        Assert.Equal(ExecutionState.Faulted, filteredEvents[5].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, filteredEvents[6].State);
        Assert.Equal(7, filteredEvents.Count);
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionShouldNotInfluenceOtherHandlers()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<SimpleJobHandler>()
                .AddJob<ExceptionJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<HandlerThatThrowsException>()
        );

        await AssertSimpleJobWasProcessedAndNotified();
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionInAsyncPartShouldNotInfluenceOtherHandlers()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<SimpleJobHandler>()
                .AddJob<ExceptionJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<HandlerThatThrowsInAsyncPartException>()
        );

        await AssertSimpleJobWasProcessedAndNotified();
    }

    private async Task AssertSimpleJobWasProcessedAndNotified()
    {
        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Assert.Single(Storage.Entries);
        Assert.Equal("Foo", Storage.Entries[0]);
    }

    private sealed class SimpleJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            context.Output = "Foo";
            return Task.CompletedTask;
        }
    }

    private sealed class SimpleJobHandler(Storage storage) : IJobNotificationHandler<SimpleJob>
    {
        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            storage.Add(context.Output!.ToString()!);
            return Task.CompletedTask;
        }
    }

    private sealed class ExceptionHandler(Storage storage) : IJobNotificationHandler<ExceptionJob>
    {
        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            Assert.NotNull(exception);

            storage.Add(exception!.GetType().Name);
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerThatThrowsException : IJobNotificationHandler<ExceptionJob>
    {
        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
            => throw new InvalidOperationException();
    }

    private sealed class HandlerThatThrowsInAsyncPartException : IJobNotificationHandler<ExceptionJob>
    {
        public async Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            throw new InvalidOperationException();
        }
    }
}
