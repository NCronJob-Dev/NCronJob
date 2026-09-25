using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class ConcurrencyTests : JobIntegrationBase
{
    [Fact]
    public async Task LongRunningJobShouldNotBlockScheduler()
    {
        var signal = new LongRunningJobSignal();
        ServiceCollection.AddSingleton(signal);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<LongRunningJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(ExecutionState.Running, 2);
        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            1);
        await signal.Started.Task.WaitAsync(CancellationToken);

        Storage.Entries.ShouldContain("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ConcurrentJobConfigurationShouldBeRespected()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ConcurrentJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute).WithName("Job 1")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 2")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 3")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 4")));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var initializingOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.Initializing,
            2);

        initializingOrchestrationEvents.Count.ShouldBe(2);
        initializingOrchestrationEvents[0].CorrelationId.ShouldNotBe(initializingOrchestrationEvents[1].CorrelationId);
    }

    [Fact]
    public async Task InstantJobHasHigherPriorityThanCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("CRON")));
        ServiceCollection.AddSingleton(new ConcurrencySettings { MaxDegreeOfParallelism = 1 });

        await StartNCronJob();

        var scheduledOrchestrationId = Events[0].CorrelationId;

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("INSTANT", CancellationToken);

        await WaitForOrchestrationCompletion(instantOrchestrationId);

        var scheduledOrchestrationEvents = Events.FilterByOrchestrationId(scheduledOrchestrationId);
        scheduledOrchestrationEvents.Select(e => e.State).ShouldBe(
            [ExecutionState.OrchestrationStarted, ExecutionState.NotStarted, ExecutionState.Scheduled]);

        var instantOrchestrationEvents = Events.FilterByOrchestrationId(instantOrchestrationId);
        instantOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: INSTANT");
        Storage.Entries.Count.ShouldBe(1);
    }

    [SupportsConcurrency(2)]
    private sealed class ConcurrentJob : DummyJob
    {
        public ConcurrentJob(Storage storage)
            : base(storage)
        {
        }
    }
}
