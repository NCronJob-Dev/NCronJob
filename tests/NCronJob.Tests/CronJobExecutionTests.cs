using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class CronJobExecutionTests : JobIntegrationBase
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCompleted<DummyJob>();
    }

    [Fact]
    public async Task CronJobScheduledWithAMacroShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression("@hourly").WithName("Hourly")));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.TryGetSchedule("Hourly", out var cronExpression, out _).ShouldBeTrue();
        cronExpression.ShouldBe("@hourly");

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromHours(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).ShouldBeScheduledThenCompleted<DummyJob>("Hourly");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ThrowingProgressCallbackDoesNotBreakJobExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        using var throwingSubscription = ServiceProvider
            .GetRequiredService<IJobExecutionProgressReporter>()
            .Register(_ => throw new InvalidOperationException("Faulty subscriber"));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).ShouldBeScheduledThenCompleted<DummyJob>();
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeStepwiseUntilStateCount(
            TimeSpan.FromMinutes(1),
            steps: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted<DummyJob>(Events, e));

        Storage.Entries.ShouldAllBe(e => e == "DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task CronJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from AddNCronJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEverySecond)));

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeStepwiseUntilStateCount(
            TimeSpan.FromSeconds(1),
            steps: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted<DummyJob>(Events, e));

        Storage.Entries.ShouldAllBe(e => e == "DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        // Auto-advancing drifts the clock by more than a second over 61 steps, which skips a second-precision slot.
        FakeTimer.AutoAdvanceAmount = TimeSpan.Zero;
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(
            p => p.WithCronExpression(Cron.AtEverySecond).WithParameter("Second")
                .And.WithCronExpression(Cron.AtEveryMinute).WithParameter("Minute")));

        await StartNCronJob();

        await AdvanceTimeStepwiseUntilStateCount(
            TimeSpan.FromSeconds(1),
            steps: 61,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 61);

        var minuteJobOutputs = Storage.Entries.Where(e => e == "DummyJob - Parameter: Minute");

        minuteJobOutputs.Count().ShouldBe(1);
        Storage.Entries.Except(minuteJobOutputs).ShouldAllBe(e => e == "DummyJob - Parameter: Second");
    }

    private static bool OrchestrationIsScheduledThenCompleted<T>(IList<ExecutionProgress> events, ExecutionProgress executionProgress)
    {
        var filteredEvents = events.FilterByOrchestrationId(executionProgress.CorrelationId);
        filteredEvents.ShouldBeScheduledThenCompleted<T>();
        return true;
    }
}
