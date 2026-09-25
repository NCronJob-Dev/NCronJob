using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobStateTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldDisableJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.DisableJob("JobName");

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCancelled<DummyJob>("JobName");
    }

    [Fact]
    public void DisablingAndEnablingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>());
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobs = jobRegistry.FindAllRootJobDefinition(typeof(DummyJob));
        jobs.Count.ShouldBe(2);

        jobs.ShouldAllBe(j => j.IsEnabled);

        registry.DisableJob<DummyJob>();

        jobs = jobRegistry.FindAllRootJobDefinition(typeof(DummyJob));
        jobs.Count.ShouldBe(2);

        jobs.ShouldAllBe(j => !j.IsEnabled);

        registry.EnableJob<DummyJob>();

        jobs.ShouldAllBe(j => j.IsEnabled);

        jobs = jobRegistry.FindAllRootJobDefinition(typeof(DummyJob));
        jobs.Count.ShouldBe(2);

        jobs.Count(j => j.CronExpression is null).ShouldBe(1);
        jobs.Count(j => j.CronExpression is not null && j.CronExpression.ToString() == Cron.AtMinute2).ShouldBe(1);
    }

    [Fact]
    public void ShouldThrowAnExceptionWhenJobIsNotFoundAndTryingToDisable()
    {
        ServiceCollection.AddNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.DisableJob("JobName"));
    }

    [Fact]
    public async Task ShouldEnableJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));

        var jobQueueManager = ServiceProvider.GetRequiredService<JobQueueManager>();

        await StartNCronJob();

        jobQueueManager.GetAllJobQueueNames().Count().ShouldBe(1);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.DisableJob("JobName");

        jobQueueManager.GetAllJobQueueNames().Count().ShouldBe(0);

        registry.EnableJob("JobName");

        jobQueueManager.GetAllJobQueueNames().Count().ShouldBe(1);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>("JobName");

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>("JobName");
    }

    [Fact]
    public async Task ShouldEnableJobWithSecondPrecision()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEverySecond).WithName("JobName")));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.DisableJob("JobName");

        Should.NotThrow(() => registry.EnableJob("JobName"));

        registry.TryGetSchedule("JobName", out var cronExpression, out _).ShouldBeTrue();
        cronExpression.ShouldBe(Cron.AtEverySecond);

        FakeTimer.Advance(TimeSpan.FromSeconds(1));

        var completed = await WaitForNthOrchestrationState(ExecutionState.Completed, 1);
        completed.Count.ShouldBe(1);
    }
}
