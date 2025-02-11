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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        events.Count.ShouldBe(0);

        Delegate jobDelegate = (Storage storage) => storage.Add("true");
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out _).ShouldBe(true);

        var orchestrationId = events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);

        filteredEvents[6].State.ShouldBe(ExecutionState.Completed);

        Storage.Entries[0].ShouldBe("true");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MultipleDynamicallyAddedJobsAreExecuted()
    {
        ServiceCollection.AddNCronJob();

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        events.Count.ShouldBe(0);

        Delegate jobDelegateOne = (Storage storage) => storage.Add("one");
        Delegate jobDelegateTwo = (Storage storage) => storage.Add("two");

        registry.TryRegister(s => s.AddJob(jobDelegateOne, Cron.AtEveryMinute), out _).ShouldBe(true);
        registry.TryRegister(s => s.AddJob(jobDelegateTwo, Cron.AtEveryMinute), out _).ShouldBe(true);

        var firstOrchestrationId = events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var secondOrchestrationId = (await WaitUntilConditionIsMet(
            events,
            ASecondOrchestrationHasCompleted)).CorrelationId;

        subscription.Dispose();

        var firstOrchestrationEvents = events.FilterByOrchestrationId(firstOrchestrationId);
        var secondOrchestrationEvents = events.FilterByOrchestrationId(secondOrchestrationId);

        firstOrchestrationEvents[6].State.ShouldBe(ExecutionState.Completed);
        secondOrchestrationEvents[6].State.ShouldBe(ExecutionState.Completed);

        Storage.Entries.ShouldContain("one");
        Storage.Entries.ShouldContain("two");
        Storage.Entries.Count.ShouldBe(2);

        ExecutionProgress? ASecondOrchestrationHasCompleted(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(e => e.CorrelationId != firstOrchestrationId && e.State == ExecutionState.OrchestrationCompleted);
        }
    }

    [Fact]
    public async Task CanRemoveJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true, CancellationToken), Cron.AtEveryMinute, jobName: "Job"));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var orchestrationId = events[0].CorrelationId;

        registry.RemoveJob("Job");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Scheduled);
        events[3].State.ShouldBe(ExecutionState.Cancelled);
        events[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(5);
        events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public void DoesNotCringeWhenRemovingNonExistingJobs()
    {
        ServiceCollection.AddNCronJob();

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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<SimpleJob>();

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Scheduled);
        events[3].State.ShouldBe(ExecutionState.Cancelled);
        events[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(5);
        events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task RemovingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression("1 * * * *")));
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllJobDefinition(typeof(SimpleJob)).Count.ShouldBe(2);

        registry.RemoveJob<SimpleJob>();

        subscription.Dispose();

        jobRegistry.FindAllJobDefinition(typeof(SimpleJob)).ShouldBeEmpty();

        List<Guid> orchestrationIds = events.Select(e => e.CorrelationId).Distinct().ToList();
        orchestrationIds.Count.ShouldBe(2);

        foreach (var orchestrationId in orchestrationIds)
        {
            List<ExecutionProgress> orchestrationEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();
            orchestrationEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
            orchestrationEvents[1].State.ShouldBe(ExecutionState.NotStarted);
            orchestrationEvents[2].State.ShouldBe(ExecutionState.Scheduled);
            orchestrationEvents[3].State.ShouldBe(ExecutionState.Cancelled);
            orchestrationEvents[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
            orchestrationEvents.Count.ShouldBe(5);
        }

        events.Count.ShouldBe(10);
    }

    [Fact]
    public async Task CanUpdateScheduleOfAJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression("0 0 * * *").WithName("JobName")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateSchedule("JobName", Cron.AtEveryMinute);

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobDefinition = jobRegistry.GetAllJobs().Single();

        jobDefinition.UserDefinedCronExpression.ShouldBe(Cron.AtEveryMinute);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Initial scheduling
        var firstOrchestrationId = events[0].CorrelationId;

        // Rescheduling
        var secondOrchestrationId = events.Skip(1).First(e => e.State == ExecutionState.OrchestrationStarted).CorrelationId;

        // Rescheduling (execution n+1)
        var thirdOrchestrationId = (await WaitUntilConditionIsMet(events, AThirdOrchestrationHasStarted)).CorrelationId;

        await WaitForOrchestrationCompletion(events, secondOrchestrationId);

        subscription.Dispose();

        var firstOrchestrationEvents = events.FilterByOrchestrationId(firstOrchestrationId);
        var secondOrchestrationEvents = events.FilterByOrchestrationId(secondOrchestrationId);
        var thirdOrchestrationEvents = events.FilterByOrchestrationId(thirdOrchestrationId);

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

        events.Count.ShouldBe(16);

        static void AssertEvent(Guid orchestrationId, ExecutionState state, ExecutionProgress executionProgress)
        {
            executionProgress.CorrelationId.ShouldBe(orchestrationId);
            executionProgress.State.ShouldBe(state);
        }

        ExecutionProgress? AThirdOrchestrationHasStarted(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(e => e.State == ExecutionState.OrchestrationStarted &&
                e.CorrelationId != firstOrchestrationId && e.CorrelationId != secondOrchestrationId);
        }

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
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEvery2ndMinute).WithName("JobName")));

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
            s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Job1"))
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
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .WithName("JobName")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var firstOrchestrationId = events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateParameter("JobName", "Bar");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, firstOrchestrationId);

        var secondOrchestrationId = (await WaitUntilConditionIsMet(events, ASecondOrchestrationHasCompleted)).CorrelationId;

        subscription.Dispose();

        var firstOrchestrationEvents = events.FilterByOrchestrationId(firstOrchestrationId);

        firstOrchestrationEvents[3].State.ShouldBe(ExecutionState.Cancelled);

        var secondOrchestrationEvents = events.FilterByOrchestrationId(secondOrchestrationId);

        secondOrchestrationEvents[6].State.ShouldBe(ExecutionState.Completed);

        Storage.Entries[0].ShouldBe("Bar");
        Storage.Entries.Count.ShouldBe(1);

        ExecutionProgress? ASecondOrchestrationHasCompleted(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(e => e.CorrelationId != firstOrchestrationId && e.State == ExecutionState.OrchestrationCompleted);
        }
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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.DisableJob("JobName");

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Scheduled);
        events[3].State.ShouldBe(ExecutionState.Cancelled);
        events[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(5);
    }

    [Fact]
    public void DisablingAndEnablingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>());
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtMinute2)));

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
    public void ShouldThrowAnExceptionWhenJobIsNotFoundAndTryingToDisable()
    {
        ServiceCollection.AddNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.DisableJob("JobName"));
    }

    [Fact]
    public async Task ShouldEnableJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var firstOrchestrationId = events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.DisableJob("JobName");

        registry.EnableJob("JobName");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var secondOrchestrationId = (await WaitUntilConditionIsMet(events, ASecondOrchestrationHasCompleted)).CorrelationId;

        subscription.Dispose();

        var firstOrchestrationEvents = events.FilterByOrchestrationId(firstOrchestrationId);

        firstOrchestrationEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        firstOrchestrationEvents[1].State.ShouldBe(ExecutionState.NotStarted);
        firstOrchestrationEvents[2].State.ShouldBe(ExecutionState.Scheduled);
        firstOrchestrationEvents[3].State.ShouldBe(ExecutionState.Cancelled);
        firstOrchestrationEvents[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        firstOrchestrationEvents.Count.ShouldBe(5);

        var secondOrchestrationEvents = events.FilterByOrchestrationId(secondOrchestrationId);

        secondOrchestrationEvents[6].State.ShouldBe(ExecutionState.Completed);
        secondOrchestrationEvents.Count.ShouldBe(8);

        ExecutionProgress? ASecondOrchestrationHasCompleted(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(e => e.CorrelationId != firstOrchestrationId && e.State == ExecutionState.OrchestrationCompleted);
        }
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

    private sealed class SimpleJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(context.Parameter?.ToString() ?? string.Empty);
            return Task.CompletedTask;
        }
    }
}
