using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobRegistryTests : JobIntegrationBase
{
    [Fact]
    public async Task DynamicallyAddedJobIsExecuted()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegate = (Storage storage) => storage.Add("true");
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out _).ShouldBe(true);

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries[0].ShouldBe("true");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MultipleDynamicallyAddedJobsAreExecuted()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegateOne = (Storage storage) => storage.Add("one");
        Delegate jobDelegateTwo = (Storage storage) => storage.Add("two");

        registry.TryRegister(s => s.AddJob(jobDelegateOne, Cron.AtEveryMinute), out _).ShouldBe(true);
        registry.TryRegister(s => s.AddJob(jobDelegateTwo, Cron.AtEveryMinute), out _).ShouldBe(true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2,
            stopMonitoringEvents: true);

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCompleted();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries.ShouldContain("one");
        Storage.Entries.ShouldContain("two");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CanRemoveJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob((Storage storage) => storage.Add("true"), Cron.AtEveryMinute, jobName: "Job"));

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var orchestrationId = Events[0].CorrelationId;

        registry.RemoveJob("Job");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCancelled("Job");
    }

    [Fact]
    public void DoesNotCringeWhenRemovingNonExistingJobs()
    {
        ServiceCollection.AddNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.GetAllJobs().ShouldBeEmpty();

        registry.RemoveJob("Nope");
        registry.RemoveJob<DummyJob>();
    }

    [Fact]
    public async Task CanRemoveByJobType()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<DummyJob>();

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

    [Fact]
    public async Task RemovingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression("1 * * * *")));
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllJobDefinition(typeof(DummyJob)).Count.ShouldBe(2);

        registry.RemoveJob<DummyJob>();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2,
            stopMonitoringEvents: true);

        jobRegistry.FindAllJobDefinition(typeof(DummyJob)).ShouldBeEmpty();

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

    [Fact]
    public async Task RemovingByJobTypeDisabledTypeJobsAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression("1 * * * *")));
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllJobDefinition(typeof(DummyJob)).Count.ShouldBe(2);

        registry.DisableJob<DummyJob>();
        registry.RemoveJob<DummyJob>();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2,
            stopMonitoringEvents: true);

        jobRegistry.FindAllJobDefinition(typeof(DummyJob)).ShouldBeEmpty();

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

    [Fact]
    public async Task CanUpdateScheduleOfAJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression("0 0 * * *").WithName("JobName")));

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateSchedule("JobName", Cron.AtEveryMinute);

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobDefinition = jobRegistry.GetAllJobs().Single();

        jobDefinition.UserDefinedCronExpression.ShouldBe(Cron.AtEveryMinute);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var startedOrchestrationEvents = await WaitForNthOrchestrationState(ExecutionState.OrchestrationStarted, 3);

        var secondOrchestrationId = startedOrchestrationEvents[1].CorrelationId;

        await WaitForOrchestrationCompletion(secondOrchestrationId, stopMonitoringEvents: true);

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
    public void ShouldRetrieveScheduleForCronJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEvery2ndMinute).WithName("JobName")));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("JobName", out var cronExpression, out var timeZoneInfo);

        successful.ShouldBeTrue();
        cronExpression.ShouldBe(Cron.AtEvery2ndMinute);
        timeZoneInfo.ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void ShouldReturnFalseIfGivenJobWasNotFound()
    {
        ServiceCollection.AddNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("JobName", out var cronExpression, out var timeZoneInfo);

        successful.ShouldBeFalse();
        cronExpression.ShouldBeNull();
        timeZoneInfo.ShouldBeNull();
    }

    [Fact]
    public void ShouldFindDependentJobsWithAGivenName()
    {
        ServiceCollection.AddNCronJob(s =>
        {
            s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Job1"))
                .ExecuteWhen(r => r.RunJob(() => { }, "Job2"));
        });

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("Job2", out var cronExpression, out var timeZoneInfo);
        successful.ShouldBeTrue();
        cronExpression.ShouldBeNull();
        timeZoneInfo.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatingParameterHasImmediateEffect()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .WithName("JobName")));

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateParameter("JobName", "Bar");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2,
            stopMonitoringEvents: true);

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>("JobName");

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>("JobName");

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Bar");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public void ShouldRetrieveAllSchedules()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p
            .WithCronExpression(Cron.AtEvery2ndMinute, timeZoneInfo: timeZone)));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.TryRegister(s => s.AddJob(() => { }, Cron.AtEveryMinute, jobName: "JobName2"), out _);

        var initialSchedules = registry.GetAllRecurringJobs();

        initialSchedules.Count.ShouldBe(2);
        initialSchedules.ShouldContain(s => s.JobName == null
                                        && s.Type == typeof(DummyJob)
                                        && s.IsTypedJob
                                        && s.CronExpression == Cron.AtEvery2ndMinute
                                        && s.IsEnabled
                                        && s.TimeZone == timeZone);
        initialSchedules.ShouldContain(s => s.JobName == "JobName2"
                                        && s.Type == null
                                        && !s.IsTypedJob
                                        && s.CronExpression == Cron.AtEveryMinute
                                        && s.IsEnabled
                                        && s.TimeZone == TimeZoneInfo.Utc);

        registry.DisableJob("JobName2");

        var newSchedules = registry.GetAllRecurringJobs();

        newSchedules.Count.ShouldBe(2);
        newSchedules.ShouldContain(s => s.JobName == null
                                        && s.Type == typeof(DummyJob)
                                        && s.IsTypedJob
                                        && s.CronExpression == Cron.AtEvery2ndMinute
                                        && s.IsEnabled
                                        && s.TimeZone == timeZone);
        newSchedules.ShouldContain(s => s.JobName == "JobName2"
                                        && s.Type == null
                                        && !s.IsTypedJob
                                        && s.CronExpression == Cron.AtEveryMinute
                                        && !s.IsEnabled
                                        && s.TimeZone == TimeZoneInfo.Utc);
    }

    [Fact]
    public void AddingJobDuringRuntimeIsRetrieved()
    {
        ServiceCollection.AddNCronJob(p => p.AddJob<DummyJob>());

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.TryRegister(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));

        var allSchedules = registry.GetAllRecurringJobs();

        allSchedules.Count.ShouldBe(1);
        allSchedules.ShouldContain(s => s.JobName == "JobName"
                                        && s.CronExpression == Cron.AtEveryMinute
                                        && s.TimeZone == TimeZoneInfo.Utc);
    }

    [Fact]
    public async Task ShouldDisableJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.DisableJob("JobName");

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

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
        var jobs = jobRegistry.FindAllJobDefinition(typeof(DummyJob));
        jobs.Count.ShouldBe(2);

        jobs.ShouldAllBe(j => j.IsEnabled);

        registry.DisableJob<DummyJob>();

        jobs = jobRegistry.FindAllJobDefinition(typeof(DummyJob));
        jobs.Count.ShouldBe(2);

        jobs.ShouldAllBe(j => !j.IsEnabled);

        registry.EnableJob<DummyJob>();

        jobs.ShouldAllBe(j => j.IsEnabled);

        jobs = jobRegistry.FindAllJobDefinition(typeof(DummyJob));
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

        await StartNCronJob(startMonitoringEvents: true);

        jobQueueManager.GetAllJobQueueNames().Count().ShouldBe(1);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.DisableJob("JobName");

        jobQueueManager.GetAllJobQueueNames().Count().ShouldBe(0);

        registry.EnableJob("JobName");

        jobQueueManager.GetAllJobQueueNames().Count().ShouldBe(1);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2,
            stopMonitoringEvents: true);

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>("JobName");

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>("JobName");

    }

    [Fact]
    public void ShouldThrowWhenDuplicateJobNamesDuringRegistration()
    {
        var act = () => ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")
            .And.WithCronExpression(Cron.AtEvery2ndMinute).WithName("JobName")));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void ShouldThrowRuntimeExceptionWithDuplicateJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));
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
        runtimeJobRegistry.TryRegister(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void TryRegisterShouldIndicateSuccess()
    {
        ServiceCollection.AddNCronJob();
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeTrue();
        exception.ShouldBeNull();
    }
}
