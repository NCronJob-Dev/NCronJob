using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobRegistryTests : JobIntegrationBase
{

    [Fact]
    public async Task DynamicallyAddedJobIsExecuted()
    {
        ServiceCollection.AddNCronJob();

        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true, CancellationToken), Cron.AtEveryMinute), out _);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task MultipleDynamicallyAddedJobsAreExecuted()
    {
        ServiceCollection.AddNCronJob();
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true, CancellationToken), Cron.AtEveryMinute));
        registry.TryRegister(s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true, CancellationToken), Cron.AtEveryMinute));

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(2);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRemoveJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true, CancellationToken), Cron.AtEveryMinute, jobName: "Job"));

        await StartAndMonitorEvents();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Guid orchestrationId = Events[0].CorrelationId;

        registry.RemoveJob("Job");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(Events, orchestrationId);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[1].State.ShouldBe(ExecutionState.NotStarted);
        Events[2].State.ShouldBe(ExecutionState.Scheduled);
        Events[3].State.ShouldBe(ExecutionState.Cancelled);
        Events[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(5);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task DoesNotCringeWhenRemovingNonExistingJobs()
    {
        ServiceCollection.AddNCronJob();

        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.GetAllJobs().ShouldBeEmpty();

        registry.RemoveJob("Nope");
        registry.RemoveJob<SimpleJob>();
    }

    [Fact]
    public async Task CanRemoveByJobType()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<SimpleJob>();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(200));
        jobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task RemovingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression("1 * * * *")));
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        await StartAndMonitorEvents();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllJobDefinition(typeof(SimpleJob)).Count.ShouldBe(2);

        List<Guid> orchestrationIds = Events.Select(e => e.CorrelationId).Distinct().ToList();
        orchestrationIds.Count.ShouldBe(2);

        foreach (var orchestrationId in orchestrationIds)
        {
            List<ExecutionProgress> orchestrationEvents = Events.Where(e => e.CorrelationId == orchestrationId).ToList();
            orchestrationEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
            orchestrationEvents[1].State.ShouldBe(ExecutionState.NotStarted);
            orchestrationEvents[2].State.ShouldBe(ExecutionState.Scheduled);
            orchestrationEvents.Count.ShouldBe(3);
        }

        registry.RemoveJob<SimpleJob>();

        jobRegistry.FindAllJobDefinition(typeof(SimpleJob)).ShouldBeEmpty();

        foreach (var orchestrationId in orchestrationIds)
        {
            List<ExecutionProgress> orchestrationEvents = Events.Where(e => e.CorrelationId == orchestrationId).ToList();
            orchestrationEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
            orchestrationEvents[1].State.ShouldBe(ExecutionState.NotStarted);
            orchestrationEvents[2].State.ShouldBe(ExecutionState.Scheduled);
            orchestrationEvents[3].State.ShouldBe(ExecutionState.Cancelled);
            orchestrationEvents[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
            orchestrationEvents.Count.ShouldBe(5);
        }

        Events.Count.ShouldBe(10);
    }

    [Fact]
    public async Task CanUpdateScheduleOfAJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression("0 0 * * *").WithName("JobName")));

        await StartAndMonitorEvents();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateSchedule("JobName", Cron.AtEveryMinute);

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobDefinition = jobRegistry.GetAllJobs().Single();

        jobDefinition.UserDefinedCronExpression.ShouldBe(Cron.AtEveryMinute);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Initial scheduling
        Guid firstOrchestrationId = Events[0].CorrelationId;

        // Rescheduling
        Guid secondOrchestrationId = Events.Skip(1).First(e => e.State == ExecutionState.OrchestrationStarted).CorrelationId;

        await WaitUntilConditionIsMet(Events, AThirdOrchestrationHasStarted);

        await WaitForOrchestrationCompletion(Events, secondOrchestrationId);

        // Rescheduling (execution n+1)
        Guid thirdOrchestrationId = Events.First(AThirdOrchestrationHasStarted).CorrelationId;

        var firstOrchestrationEvents = Events.Where(e => e.CorrelationId == firstOrchestrationId).ToList();
        var secondOrchestrationEvents = Events.Where(e => e.CorrelationId == secondOrchestrationId).ToList();
        var thirdOrchestrationEvents = Events.Where(e => e.CorrelationId == thirdOrchestrationId).ToList();

        // Initial scheduling
        AssertEvent(firstOrchestrationId, ExecutionState.OrchestrationStarted, firstOrchestrationEvents[0]);
        AssertEvent(firstOrchestrationId, ExecutionState.NotStarted, firstOrchestrationEvents[1]);
        AssertEvent(firstOrchestrationId, ExecutionState.Scheduled, firstOrchestrationEvents[2]);
        AssertEvent(firstOrchestrationId, ExecutionState.Cancelled, firstOrchestrationEvents[3]);
        AssertEvent(firstOrchestrationId, ExecutionState.OrchestrationCompleted, firstOrchestrationEvents[4]);

        // Rescheduling
        AssertEvent(secondOrchestrationId, ExecutionState.OrchestrationStarted, secondOrchestrationEvents[0]);
        AssertEvent(secondOrchestrationId, ExecutionState.NotStarted, secondOrchestrationEvents[1]);
        AssertEvent(secondOrchestrationId, ExecutionState.Scheduled, secondOrchestrationEvents[2]);
        AssertEvent(secondOrchestrationId, ExecutionState.Initializing, secondOrchestrationEvents[3]);
        AssertEvent(secondOrchestrationId, ExecutionState.Running, secondOrchestrationEvents[4]);
        AssertEvent(secondOrchestrationId, ExecutionState.Completing, secondOrchestrationEvents[5]);
        AssertEvent(secondOrchestrationId, ExecutionState.Completed, secondOrchestrationEvents[6]);
        AssertEvent(secondOrchestrationId, ExecutionState.OrchestrationCompleted, secondOrchestrationEvents[7]);

        // Rescheduling (execution n+1)
        AssertEvent(thirdOrchestrationId, ExecutionState.OrchestrationStarted, thirdOrchestrationEvents[0]);
        AssertEvent(thirdOrchestrationId, ExecutionState.NotStarted, thirdOrchestrationEvents[1]);
        AssertEvent(thirdOrchestrationId, ExecutionState.Scheduled, thirdOrchestrationEvents[2]);

        Events.Count.ShouldBe(16);

        static void AssertEvent(Guid orchestrationId, ExecutionState state, ExecutionProgress executionProgress)
        {
            executionProgress.CorrelationId.ShouldBe(orchestrationId);
            executionProgress.State.ShouldBe(state);
        }

        bool AThirdOrchestrationHasStarted(ExecutionProgress e)
        {
            return e.State == ExecutionState.OrchestrationStarted &&
                e.CorrelationId != firstOrchestrationId && e.CorrelationId != secondOrchestrationId;
        }

    }

    [Fact]
    public async Task ShouldThrowAnExceptionWhenJobIsNotFoundAndTryingToUpdateSchedule()
    {
        ServiceCollection.AddNCronJob();
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.UpdateSchedule("JobName", Cron.AtEveryMinute));
    }

    [Fact]
    public async Task ShouldRetrieveScheduleForCronJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEvery2ndMinute).WithName("JobName")));
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("JobName", out var cronExpression, out var timeZoneInfo);

        successful.ShouldBeTrue();
        cronExpression.ShouldBe(Cron.AtEvery2ndMinute);
        timeZoneInfo.ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public async Task ShouldReturnFalseIfGivenJobWasNotFound()
    {
        ServiceCollection.AddNCronJob();
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("JobName", out var cronExpression, out var timeZoneInfo);

        successful.ShouldBeFalse();
        cronExpression.ShouldBeNull();
        timeZoneInfo.ShouldBeNull();
    }

    [Fact]
    public async Task ShouldFindDependentJobsWithAGivenName()
    {
        ServiceCollection.AddNCronJob(s =>
        {
            s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Job1"))
                .ExecuteWhen(r => r.RunJob(() => { }, "Job2"));
        });
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("Job2", out var cronExpression, out var timeZoneInfo);
        successful.ShouldBeTrue();
        cronExpression.ShouldBeNull();
        timeZoneInfo.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatingParameterHasImmediateEffect()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .WithName("JobName")));
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateParameter("JobName", "Bar");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Bar");
    }

    [Fact]
    public void ShouldRetrieveAllSchedules()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p
            .WithCronExpression(Cron.AtEvery2ndMinute, timeZoneInfo: timeZone)
            .WithName("JobName")));
        
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.TryRegister(s => s.AddJob(() => { }, Cron.AtEveryMinute, jobName: "JobName2"), out _);

        var allSchedules = registry.GetAllRecurringJobs();

        allSchedules.Count.ShouldBe(2);
        allSchedules.ShouldContain(s => s.JobName == "JobName"
                                        && s.CronExpression == Cron.AtEvery2ndMinute
                                        && s.TimeZone == timeZone);
        allSchedules.ShouldContain(s => s.JobName == "JobName2"
                                        && s.CronExpression == Cron.AtEveryMinute
                                        && s.TimeZone == TimeZoneInfo.Utc);
    }

    [Fact]
    public void AddingJobDuringRuntimeIsRetrieved()
    {
        ServiceCollection.AddNCronJob(p => p.AddJob<SimpleJob>());
        
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.TryRegister(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));

        var allSchedules = registry.GetAllRecurringJobs();

        allSchedules.Count.ShouldBe(1);
        allSchedules.ShouldContain(s => s.JobName == "JobName"
                                        && s.CronExpression == Cron.AtEveryMinute
                                        && s.TimeZone == TimeZoneInfo.Utc);
    }

    [Fact]
    public async Task ShouldDisableJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.DisableJob("JobName");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(200));
        jobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task DisablingAndEnablingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>());
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobs = jobRegistry.FindAllJobDefinition(typeof(SimpleJob));
        jobs.Count.ShouldBe(2);

        jobs.ShouldAllBe(j => j.CronExpression == null || j.CronExpression != RuntimeJobRegistry.TheThirtyFirstOfFebruary);

        registry.DisableJob<SimpleJob>();

        jobs = jobRegistry.FindAllJobDefinition(typeof(SimpleJob));
        jobs.Count.ShouldBe(2);

        jobs.ShouldAllBe(j => j.CronExpression == RuntimeJobRegistry.TheThirtyFirstOfFebruary);

        registry.EnableJob<SimpleJob>();

        jobs = jobRegistry.FindAllJobDefinition(typeof(SimpleJob));
        jobs.Count.ShouldBe(2);

        jobs.Count(j => j.CronExpression is null).ShouldBe(1);
        jobs.Count(j => j.CronExpression is not null && j.CronExpression.ToString() == Cron.AtMinute2).ShouldBe(1);
    }

    [Fact]
    public async Task ShouldThrowAnExceptionWhenJobIsNotFoundAndTryingToDisable()
    {
        ServiceCollection.AddNCronJob();
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.DisableJob("JobName"));
    }

    [Fact]
    public async Task ShouldEnableJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));
        
        await StartAndMonitorEvents();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.DisableJob("JobName");

        registry.EnableJob("JobName");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public void ShouldThrowWhenDuplicateJobNamesDuringRegistration()
    {
        var act = () => ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")
            .And.WithCronExpression(Cron.AtEvery2ndMinute).WithName("JobName")));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void ShouldThrowRuntimeExceptionWithDuplicateJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob(() =>
        {
        }, Cron.AtEveryMinute, jobName: "JobName"), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TryRegisteringShouldIndicateFailureWithAGivenException()
    {
        ServiceCollection.AddNCronJob();
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        runtimeJobRegistry.TryRegister(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void TryRegisterShouldIndicateSuccess()
    {
        ServiceCollection.AddNCronJob();
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeTrue();
        exception.ShouldBeNull();
    }

    private sealed class SimpleJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token) => await writer.WriteAsync(context.Parameter ?? string.Empty, token);
    }
}
