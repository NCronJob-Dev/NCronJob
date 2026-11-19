using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class JobFaultIsolationTests : JobIntegrationBase
{
    [Fact]
    public async Task AsyncVoidDelegateJobIsRejectedInsteadOfRunningUnobserved()
    {
        ServiceCollection.AddNCronJob(_ => { });
        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .ForceRunInstantJob(AsyncVoidJob, CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).ShouldContain(e => e.State == ExecutionState.Faulted);
        Storage.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotificationHandlerFailingToConstructDoesNotFailJobOrSkipDependents()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<DummyJob>()
            .AddNotificationHandler<NotificationHandlerThrowingInCtor>()
            .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>()));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .ForceRunInstantJob<DummyJob>(token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).ShouldNotContain(e => e.State == ExecutionState.Faulted);
        Storage.Entries.ShouldBe(["DummyJob - Parameter: ", "AnotherDummyJob - Parameter: "], ignoreOrder: false);
    }

    [Fact]
    public async Task ScopedServiceFailingToDisposeDoesNotFailCompletedJob()
    {
        ServiceCollection.AddScoped<ServiceThrowingOnDispose>();
        ServiceCollection.AddNCronJob(n => n.AddJob<JobUsingServiceThrowingOnDispose>());

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .ForceRunInstantJob<JobUsingServiceThrowingOnDispose>(token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var events = Events.FilterByOrchestrationId(orchestrationId);
        events.ShouldContain(e => e.State == ExecutionState.Completed);
        events.ShouldNotContain(e => e.State == ExecutionState.Faulted);
    }

    private static async void AsyncVoidJob(Storage storage)
    {
        await Task.Yield();
        storage.Add("async void job ran");
    }

    private sealed class NotificationHandlerThrowingInCtor : IJobNotificationHandler<DummyJob>
    {
        public NotificationHandlerThrowingInCtor() => throw new InvalidOperationException();

        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ServiceThrowingOnDispose : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => throw new InvalidOperationException();
    }

    private sealed class JobUsingServiceThrowingOnDispose(ServiceThrowingOnDispose service) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            GC.KeepAlive(service);
            return Task.CompletedTask;
        }
    }
}
