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
            .WithName("Job 1").WithCronExpression(Cron.AtEveryMinute)
            .And.WithName("Job 2").WithCronExpression(Cron.AtEveryMinute)
            .And.WithName("Job 3").WithCronExpression(Cron.AtEveryMinute)
            .And.WithName("Job 4").WithCronExpression(Cron.AtEveryMinute)));

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

    [Fact]
    public async Task CronRunDueWhileJobStillRunsIsSkippedWhenOptedIn()
    {
        var signal = new LongRunningJobSignal();
        ServiceCollection.AddSingleton(signal);
        ServiceCollection.AddNCronJob(n => n.AddJob<LongRunningJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute)
            .SkipIfStillRunning()));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await signal.Started.Task.WaitAsync(CancellationToken);
        await WaitForNthOrchestrationState(ExecutionState.Scheduled, 2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForNthOrchestrationState(ExecutionState.Skipped, 1);
        await WaitForNthOrchestrationState(ExecutionState.Scheduled, 3);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForNthOrchestrationState(ExecutionState.Skipped, 2);

        Storage.Entries.ShouldBe(["Running LongRunningJob"]);
    }

    [Fact]
    public async Task CronRunIsNotSkippedWhenPreviousRunHasFinished()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute)
            .SkipIfStillRunning()));

        await StartNCronJob();
        var limiter = ServiceProvider.GetRequiredService<JobConcurrencyLimiter>();
        var job = ServiceProvider.GetRequiredService<JobRegistry>().GetAllCronJobs().Single();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForNthOrchestrationState(ExecutionState.Completed, 1);
        await WaitUntilReleasedAsync(limiter, job);
        await WaitForNthOrchestrationState(ExecutionState.Scheduled, 2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForNthOrchestrationState(ExecutionState.Completed, 2);

        Events.ShouldNotContain(e => e.State == ExecutionState.Skipped);
    }

    // The slot is released only after Completed is reported, so the next occurrence could otherwise still see it taken.
    private async Task WaitUntilReleasedAsync(JobConcurrencyLimiter limiter, JobDefinition job)
    {
        while (true)
        {
            var released = limiter.WaitForReleaseAsync();
            if (!limiter.IsJobAtCapacity(job))
            {
                return;
            }

            await released.WaitAsync(CancellationToken);
        }
    }

    [Fact]
    public void JobIsAtCapacityOnlyWhenItsOwnLimitIsReached()
    {
        var limiter = new JobConcurrencyLimiter(new ConcurrencySettings { MaxDegreeOfParallelism = 1 });
        var running = JobDefinition.CreateTyped(typeof(DummyJob), null);
        var waiting = JobDefinition.CreateTyped(typeof(LongRunningJob), null);

        limiter.TryAcquire(running).ShouldBeTrue();

        limiter.IsJobAtCapacity(running).ShouldBeTrue();
        limiter.TryAcquire(waiting).ShouldBeFalse();
        limiter.IsJobAtCapacity(waiting).ShouldBeFalse();

        limiter.Release(running);
        limiter.IsJobAtCapacity(running).ShouldBeFalse();
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
