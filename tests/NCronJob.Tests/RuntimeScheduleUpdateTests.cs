using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeScheduleUpdateTests : JobIntegrationBase
{
    [Fact]
    public async Task CanUpdateScheduleOfAJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression("0 0 * * *").WithName("JobName")));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateSchedule("JobName", Cron.AtEveryMinute);

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobDefinition = jobRegistry.GetAllRootJobs().Single();

        jobDefinition.UserDefinedCronExpression.ShouldBe(Cron.AtEveryMinute);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var startedOrchestrationEvents = await WaitForNthOrchestrationState(ExecutionState.OrchestrationStarted, 3);

        var secondOrchestrationId = startedOrchestrationEvents[1].CorrelationId;

        await WaitForOrchestrationCompletion(secondOrchestrationId);

        // Initial scheduling
        var firstOrchestrationEvents = Events.FilterByOrchestrationId(startedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>("JobName");

        // Rescheduling
        var secondOrchestrationEvents = Events.FilterByOrchestrationId(secondOrchestrationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>("JobName");

        // Rescheduling (execution n+1)
        var thirdOrchestrationEvents = Events.FilterByOrchestrationId(startedOrchestrationEvents[2].CorrelationId);
        thirdOrchestrationEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        thirdOrchestrationEvents[1].State.ShouldBe(ExecutionState.NotStarted);
        thirdOrchestrationEvents[2].State.ShouldBe(ExecutionState.Scheduled);

        Events.Count.ShouldBe(16);
    }

    [Fact]
    public void ShouldThrowAnExceptionWhenJobIsNotFoundAndTryingToUpdateSchedule()
    {
        ServiceCollection.AddNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.UpdateSchedule("JobName", Cron.AtEveryMinute));
    }

    [Fact]
    public async Task UpdatingParameterHasImmediateEffect()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .WithName("JobName")));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateParameter("JobName", "Bar");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>("JobName");

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>("JobName");

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Bar");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public void UpdatingParameterCanSetAndClearConfiguredParameter()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .WithName("JobName")));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobDefinition = ServiceProvider.GetRequiredService<JobRegistry>().FindRootJobDefinition("JobName");
        jobDefinition.ShouldNotBeNull();

        registry.UpdateParameter("JobName", "bar");
        jobDefinition.Parameter.ShouldBe("bar");

        registry.UpdateParameter("JobName", null);
        jobDefinition.Parameter.ShouldBeNull();
    }
}
