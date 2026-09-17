using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class NotificationHandlerTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldCallNotificationHandlerWhenJobIsDone()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<DummyJobHandler>()
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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("ExceptionHandler - Exception: InvalidOperationException");
        Storage.Entries.Count.ShouldBe(1);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringRun<ExceptionJob>();
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionShouldNotInfluenceOtherHandlers()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<DummyJobHandler>()
                .AddJob<ExceptionJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<HandlerThatThrowsException>()
        );

        await StartNCronJobAndAssertSimpleJobWasProcessedAndNotified();
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionInAsyncPartShouldNotInfluenceOtherHandlers()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<DummyJobHandler>()
                .AddJob<ExceptionJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<HandlerThatThrowsInAsyncPartException>()
        );

        await StartNCronJobAndAssertSimpleJobWasProcessedAndNotified();
    }

    [Fact]
    public async Task HandlerThrowingAggregateExceptionDoesNotFailTheJob()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddNotificationHandler<HandlerThatThrowsAggregateException>()
        );

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).ShouldBeScheduledThenCompleted<DummyJob>();
        Storage.Entries.ShouldBe(["DummyJob - Parameter: ", "HandlerThatThrowsAggregateException"], ignoreOrder: false);
    }

    private async Task StartNCronJobAndAssertSimpleJobWasProcessedAndNotified()
    {
        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("DummyJobHandler - Output: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    private sealed class DummyJobHandler(Storage storage) : IJobNotificationHandler<DummyJob>
    {
        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            storage.Add($"{GetType().Name} - Output: {context.Output}");
            return Task.CompletedTask;
        }
    }

    private sealed class ExceptionHandler(Storage storage) : IJobNotificationHandler<ExceptionJob>
    {
        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            exception.ShouldNotBeNull();

            storage.Add($"{GetType().Name} - Exception: {exception.GetType().Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerThatThrowsAggregateException(Storage storage) : IJobNotificationHandler<DummyJob>
    {
        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            storage.Add(GetType().Name);
            throw new AggregateException(new InvalidOperationException());
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
