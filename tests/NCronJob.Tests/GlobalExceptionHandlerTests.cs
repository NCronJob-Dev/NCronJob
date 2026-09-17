using Shouldly;

namespace NCronJob.Tests;

public sealed class GlobalExceptionHandlerTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldInformGlobalExceptionHandlerInOrder()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<FirstTestExceptionHandler>();
            o.AddExceptionHandler<SecondTestExceptionHandler>();
            o.AddJob<ExceptionJob>(jo => jo.WithCronExpression(Cron.AtEveryMinute));
        });

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringRun<ExceptionJob>();

        Storage.Entries[0].ShouldBe("1");
        Storage.Entries[1].ShouldBe("2");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ShouldStopProcessingWhenHandlerReturnsTrue()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<FirstHandlerThatStops>();
            o.AddExceptionHandler<SecondTestExceptionHandler>();
            o.AddJob<ExceptionJob>(jo => jo.WithCronExpression(Cron.AtEveryMinute));
        });

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringRun<ExceptionJob>();

        Storage.Entries[0].ShouldBe("1");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExceptionHandlerThatThrowsShouldntStopProcessing()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<ExceptionHandlerThatThrows>();
            o.AddExceptionHandler<SecondTestExceptionHandler>();
            o.AddJob<ExceptionJob>(jo => jo.WithCronExpression(Cron.AtEveryMinute));
        });

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringRun<ExceptionJob>();

        Storage.Entries[0].ShouldBe("boom");
        Storage.Entries[1].ShouldBe("2");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task JobThatThrowsWhenCreatedIsCaughtByGlobalExceptionHandler()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<FirstTestExceptionHandler>();
            o.AddJob<JobThatThrowsInCtor>(b => b.WithCronExpression(Cron.AtEveryMinute));
        });

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringInitialization<JobThatThrowsInCtor>();

        Storage.Entries[0].ShouldBe("1");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task JobThrowingItsOwnCancellationIsFaultedAndHandled()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<FirstTestExceptionHandler>();
            o.AddJob<InternalTimeoutJob>(jo => jo.WithCronExpression(Cron.AtEveryMinute))
                .ExecuteWhen(
                    success: s => s.RunJob((Storage storage) => storage.Add("success")),
                    faulted: s => s.RunJob((Storage storage) => storage.Add("faulted")));
        });

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var rootJobEvents = Events.FilterByOrchestrationId(orchestrationId).Where(e => e.Type == typeof(InternalTimeoutJob)).ToList();
        rootJobEvents.ShouldContain(e => e.State == ExecutionState.Faulted);
        rootJobEvents.ShouldNotContain(e => e.State == ExecutionState.Completed);

        Storage.Entries.ShouldBe(["1", "faulted"], ignoreOrder: false);
    }

    [Fact]
    public async Task JobThrowingAggregateExceptionIsHandled()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<FirstTestExceptionHandler>();
            o.AddJob<AggregateExceptionJob>(jo => jo.WithCronExpression(Cron.AtEveryMinute))
                .ExecuteWhen(faulted: s => s.RunJob((Storage storage) => storage.Add("faulted")));
        });

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId)
            .ShouldContain(e => e.Type == typeof(AggregateExceptionJob) && e.State == ExecutionState.Faulted);

        Storage.Entries.ShouldBe(["1", "faulted"], ignoreOrder: false);
    }

    private sealed class InternalTimeoutJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => throw new TaskCanceledException("Simulated HttpClient timeout");
    }

    private sealed class AggregateExceptionJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => throw new AggregateException(new InvalidOperationException());
    }

    private sealed class FirstTestExceptionHandler(Storage storage) : IExceptionHandler
    {
        public Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            storage.Add("1");
            return Task.FromResult(false);
        }
    }

    private sealed class SecondTestExceptionHandler(Storage storage) : IExceptionHandler
    {
        public Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            storage.Add("2");
            return Task.FromResult(false);
        }
    }

    private sealed class FirstHandlerThatStops(Storage storage) : IExceptionHandler
    {
        public Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            storage.Add("1");
            return Task.FromResult(true);
        }
    }

    private sealed class ExceptionHandlerThatThrows(Storage storage) : IExceptionHandler
    {
        public Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            storage.Add("boom");
            throw new InvalidOperationException();
        }
    }
}
