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

        await StartNCronJobAndAssertSimpleJobWasProcessedAndNotified();
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

        var orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("InvalidOperationException");
        Storage.Entries.Count.ShouldBe(1);

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);

        filteredEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        filteredEvents[1].State.ShouldBe(ExecutionState.NotStarted);
        filteredEvents[2].State.ShouldBe(ExecutionState.Scheduled);
        filteredEvents[3].State.ShouldBe(ExecutionState.Initializing);
        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Faulted);
        filteredEvents[6].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        filteredEvents.Count.ShouldBe(7);
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

        await StartNCronJobAndAssertSimpleJobWasProcessedAndNotified();
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

        await StartNCronJobAndAssertSimpleJobWasProcessedAndNotified();
    }

    private async Task StartNCronJobAndAssertSimpleJobWasProcessedAndNotified()
    {
        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("Foo");
        Storage.Entries.Count.ShouldBe(1);
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
            exception.ShouldNotBeNull();

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
