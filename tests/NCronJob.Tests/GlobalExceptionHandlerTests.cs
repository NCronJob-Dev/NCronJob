using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            o.AddJob(() =>
            {
                throw new InvalidOperationException();
            }, Cron.AtEveryMinute);
        });

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        List<ExecutionProgress> filteredEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();

        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Faulted);

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
            o.AddJob(() =>
            {
                throw new InvalidOperationException();
            }, Cron.AtEveryMinute);
        });

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        List<ExecutionProgress> filteredEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();

        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Faulted);

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
            o.AddJob(() =>
            {
                throw new InvalidOperationException();
            }, Cron.AtEveryMinute);
        });

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        List<ExecutionProgress> filteredEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();

        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Faulted);

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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        List<ExecutionProgress> filteredEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();

        filteredEvents[3].State.ShouldBe(ExecutionState.Initializing);
        filteredEvents[4].State.ShouldBe(ExecutionState.Faulted);

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
