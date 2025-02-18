using Microsoft.Extensions.DependencyInjection;
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

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringRun();

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

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringRun();

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

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringRun();

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

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenFaultedDuringInitialization();

        Storage.Entries[0].ShouldBe("1");
        Storage.Entries.Count.ShouldBe(1);
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
