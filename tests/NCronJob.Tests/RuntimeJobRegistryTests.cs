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

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegate = (Storage storage) => storage.Add("true");
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out _).ShouldBe(true);

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries[0].ShouldBe("true");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MultipleDynamicallyAddedJobsAreExecuted()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegateOne = (Storage storage) => storage.Add("one");
        Delegate jobDelegateTwo = (Storage storage) => storage.Add("two");

        registry.TryRegister(s => s.AddJob(jobDelegateOne, Cron.AtEveryMinute), out _).ShouldBe(true);
        registry.TryRegister(s => s.AddJob(jobDelegateTwo, Cron.AtEveryMinute), out _).ShouldBe(true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCompleted();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries.ShouldContain("one");
        Storage.Entries.ShouldContain("two");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CanRegisterMultipleTimesTheSameDelegateWithDifferentNames()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegate = () => { };

        // Trying to register twice the same unnamed delegates fails
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out _).ShouldBe(true);

        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out var unNamedUntypedJobException).ShouldBe(false);
        unNamedUntypedJobException.ShouldNotBeNull();
        unNamedUntypedJobException.Message.ShouldStartWith("Job registration conflict for job 'Untyped job NCronJob.UntypedJob_I2s40FrC' detected.");

        // Trying to register twice the same named delegates fails as well
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute, jobName: "one"), out _).ShouldBe(true);
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute, jobName: "one"), out var namedUntypedJobException).ShouldBe(false);
        namedUntypedJobException.ShouldNotBeNull();
        namedUntypedJobException.Message.ShouldStartWith("Job registration conflict detected. A job has already been registered with the name 'one'.");

        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute, jobName: "another"), out _).ShouldBe(true);

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.GetAllRootJobs().Select(jd => jd.JobFullName).ShouldBe(["Untyped job NCronJob.UntypedJob_I2s40FrC", "Untyped job one", "Untyped job another"]);
    }

    [Fact]
    public async Task CanRemoveJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob((Storage storage) => storage.Add("true"), Cron.AtEveryMinute, jobName: "Job"));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var orchestrationId = Events[0].CorrelationId;

        registry.RemoveJob("Job");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCancelled("Job");
    }

    [Fact]
    public async Task RemovingAJobStopsItsQueueWorker()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var queueWorker = ServiceProvider.GetServices<IHostedService>().OfType<QueueWorker>().Single();

        registry.TryRegister(s => s.AddJob((Storage storage) => storage.Add("true"), Cron.AtEveryMinute, jobName: "Job"), out _).ShouldBeTrue();
        var queueName = queueWorker.GetActiveWorkerQueueNames().Single();

        registry.RemoveJob("Job");

        await queueWorker.WaitForWorkerRemovalAsync(queueName, CancellationToken);

        ServiceProvider.GetRequiredService<JobQueueManager>().GetAllJobQueueNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposingQueueWorkerStopsPendingWorkerRemovalWait()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob((Storage storage) => storage.Add("true"), Cron.AtEveryMinute, jobName: "Job"));

        await StartNCronJob();

        var queueWorker = ServiceProvider.GetServices<IHostedService>().OfType<QueueWorker>().Single();
        var queueName = queueWorker.GetActiveWorkerQueueNames().Single();
        var wait = queueWorker.WaitForWorkerRemovalAsync(queueName, CancellationToken);

        queueWorker.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(wait);
    }

    [Fact]
    public void CannotRemoveAUntypedDependentJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob(() => { }, "Job"))
        );

        var jobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var act = () => jobRegistry.RemoveJob("Job");

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldBe("Cannot remove a job that is a dependency of another job.");
    }

    [Fact]
    public void CannotRemoveATypedDependentJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>())
        );

        var jobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var act = () => jobRegistry.RemoveJob<AnotherDummyJob>();

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldBe("Cannot remove a job that is a dependency of another job.");
    }

    [Fact]
    public void DoesNotCringeWhenRemovingNonExistingJobs()
    {
        ServiceCollection.AddNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.GetAllRootJobs().ShouldBeEmpty();

        registry.RemoveJob("Nope");
        registry.RemoveJob<DummyJob>();
    }

    [Fact]
    public async Task CanRemoveByJobType()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<DummyJob>();

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

    [Fact]
    public async Task RemovingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression("1 * * * *")));
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).Count.ShouldBe(2);

        registry.RemoveJob<DummyJob>();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).ShouldBeEmpty();

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

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).Count.ShouldBe(2);

        registry.DisableJob<DummyJob>();
        registry.RemoveJob<DummyJob>();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).ShouldBeEmpty();

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

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
    public void ADependentJobHasNoSchedule()
    {
        ServiceCollection.AddNCronJob(s =>
        {
            s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Job1"))
                .ExecuteWhen(r => r.RunJob(() => { }, "Job2"));
        });

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("Job2", out var _, out var _);
        successful.ShouldBeFalse();

        var act1 = () => registry.DisableJob("Job2");
        act1.ShouldThrow<InvalidOperationException>()
            .Message.ShouldBe("Root job with name 'Job2' not found.");

        var act2 = () => registry.EnableJob("Job2");
        act2.ShouldThrow<InvalidOperationException>()
            .Message.ShouldBe("Root job with name 'Job2' not found.");
    }

    [Fact]
    public void ARootJobCanHaveNoSchedule()
    {
        ServiceCollection.AddNCronJob(s =>
        {
            s.AddJob<DummyJob>(p => p.WithName("Job1"));
        });

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("Job1", out var cronExpression, out var timeZoneInfo);
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

    [Fact]
    public async Task ConcurrentRuntimeRegistrationsAndReadsAreThreadSafe()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        const int jobCount = 200;

        var writer = Task.Run(() => Parallel.For(0, jobCount, i =>
            registry.TryRegister(s => s.AddJob(() => { }, Cron.AtEveryMinute, jobName: $"Job{i}"), out _).ShouldBeTrue()),
            CancellationToken);

        var reader = Task.Run(() =>
        {
            while (!writer.IsCompleted)
            {
                _ = registry.GetAllRecurringJobs();
                _ = registry.TryGetSchedule("Job0", out _, out _);
            }
        }, CancellationToken);

        await Task.WhenAll(writer, reader);

        registry.GetAllRecurringJobs().Count.ShouldBe(jobCount);

        var jobQueueManager = ServiceProvider.GetRequiredService<JobQueueManager>();
        for (var i = 0; i < jobCount; i++)
        {
            jobQueueManager.TryGetQueue($"Untyped job Job{i}", out var jobQueue).ShouldBeTrue();
            jobQueue.Count.ShouldBe(1);
        }
    }

    [Fact]
    public void ReschedulingAJobThatIsNoLongerRegisteredDoesNotRecreateItsQueue()
    {
        ServiceCollection.AddNCronJob();

        var orphan = JobDefinition.CreateUntyped("Orphan", () => { });
        orphan.UpdateWith(new JobOption { CronExpression = Cron.AtEveryMinute });

        ServiceProvider.GetRequiredService<JobWorker>().ScheduleJob(orphan);

        ServiceProvider.GetRequiredService<JobQueueManager>().GetAllJobQueueNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task StoppingWaitsForRunningJobsOfRemovedQueues()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ServiceCollection.AddSingleton(gate);
        ServiceCollection.AddNCronJob(s => s.AddJob<GatedJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(ExecutionState.Running, 1);

        ServiceProvider.GetRequiredService<IRuntimeJobRegistry>().RemoveJob<GatedJob>();

        var queueWorker = ServiceProvider.GetServices<IHostedService>().OfType<QueueWorker>().Single();
        var stopTask = queueWorker.StopAsync(CancellationToken);

        stopTask.IsCompleted.ShouldBeFalse();

        gate.SetResult();
        await stopTask;
    }

    [Fact]
    public async Task ProgressCallbackCanUseTheRegistryWhileAJobIsBeingRemoved()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var registeredFromCallback = false;

        using var subscription = ServiceProvider.GetRequiredService<IJobExecutionProgressReporter>().Register(progress =>
        {
            if (progress.State != ExecutionState.Cancelled)
            {
                return;
            }

            registeredFromCallback = registry.TryRegister(
                s => s.AddJob(() => { }, Cron.AtEveryMinute, jobName: "FromCallback"));
        });

        registry.RemoveJob("JobName");

        registeredFromCallback.ShouldBeTrue();
    }

    private sealed class GatedJob(TaskCompletionSource gate) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => gate.Task;
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
    public void LateRegistrationConflictDoesNotPartiallyRegisterOrScheduleEarlierJobs()
    {
        ServiceCollection.AddNCronJob();

        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var queueManager = ServiceProvider.GetRequiredService<JobQueueManager>();

        var successful = runtimeJobRegistry.TryRegister(s =>
        {
            s.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Duplicate"));
            s.AddJob(() => { }, Cron.AtEveryMinute, jobName: "Duplicate");
        }, out var exception);

        successful.ShouldBeFalse();
        exception.ShouldBeOfType<InvalidOperationException>();
        jobRegistry.GetAllRootJobs().ShouldBeEmpty();
        queueManager.GetAllJobQueueNames().ShouldBeEmpty();
        queueManager.TryGetQueue(typeof(AnotherDummyJob).FullName!, out _).ShouldBeFalse();

        runtimeJobRegistry.TryRegister(
            s => s.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Duplicate")),
            out _).ShouldBeTrue();

        var registeredJob = jobRegistry.FindRootJobDefinition("Duplicate");
        registeredJob.ShouldNotBeNull();
    }

    [Fact]
    public void SchedulingFailureRollsBackRegistryQueuesDependenciesAndServices()
    {
        ServiceCollection.AddNCronJob(s =>
            s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2).WithName("Existing"))
                .ExecuteWhen(success: d => d.RunJob<ExceptionJob>()));

        var serviceDescriptors = ServiceCollection.ToArray();
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobWorker = ServiceProvider.GetRequiredService<JobWorker>();
        var queueManager = ServiceProvider.GetRequiredService<JobQueueManager>();
        var settings = ServiceProvider.GetRequiredService<ConcurrencySettings>();
        var previousMaxDegreeOfParallelism = settings.MaxDegreeOfParallelism;
        var previousDefaultJobRunExpiry = settings.DefaultJobRunExpiry;
        var existingJob = jobRegistry.FindRootJobDefinition("Existing");
        existingJob.ShouldNotBeNull();

        jobWorker.ScheduleJob(existingJob);
        queueManager.TryGetQueue(typeof(DummyJob).FullName!, out var sharedQueue).ShouldBeTrue();
        var existingRun = sharedQueue.Single();

        void ThrowOnNewQueue(string _) => throw new InvalidOperationException("Scheduling failed.");

        queueManager.QueueAdded += ThrowOnNewQueue;
        var successful = runtimeJobRegistry.TryRegister(builder =>
        {
            var fullBuilder = (NCronJobOptionBuilder)builder;
            fullBuilder
                .WithMaxDegreeOfParallelism(previousMaxDegreeOfParallelism + 1)
                .WithDefaultJobRunExpiry(TimeSpan.FromMinutes(3));
            fullBuilder.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute5).WithName("Batch"))
                .AddNotificationHandler<RollbackNotificationHandler>()
                .ExecuteWhen(success: d => d.RunJob<AnotherDummyJob>());
            fullBuilder.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Failure"));
        }, out var exception);
        queueManager.QueueAdded -= ThrowOnNewQueue;

        successful.ShouldBeFalse();
        exception.ShouldBeOfType<InvalidOperationException>();
        jobRegistry.GetAllRootJobs().ShouldBe([existingJob]);
        jobRegistry.GetDependentSuccessJobs(existingJob).Single().Type.ShouldBe(typeof(ExceptionJob));

        queueManager.TryGetQueue(typeof(DummyJob).FullName!, out sharedQueue).ShouldBeTrue();
        sharedQueue.Count.ShouldBe(1);
        sharedQueue.Single().ShouldBeSameAs(existingRun);
        queueManager.TryGetQueue(typeof(AnotherDummyJob).FullName!, out _).ShouldBeFalse();

        ServiceCollection.Count.ShouldBe(serviceDescriptors.Length);
        ServiceCollection.Zip(serviceDescriptors).ShouldAllBe(pair => ReferenceEquals(pair.First, pair.Second));
        settings.MaxDegreeOfParallelism.ShouldBe(previousMaxDegreeOfParallelism);
        settings.DefaultJobRunExpiry.ShouldBe(previousDefaultJobRunExpiry);

        runtimeJobRegistry.TryRegister(
            builder => builder.AddJob(
                typeof(DummyJob),
                p => p.WithCronExpression(Cron.AtMinute5).WithName("Batch")),
            out _).ShouldBeTrue();

        var reRegisteredJob = jobRegistry.FindRootJobDefinition("Batch");
        reRegisteredJob.ShouldNotBeNull();
        jobRegistry.GetDependentSuccessJobs(reRegisteredJob).ShouldBeEmpty();
    }

    [Fact]
    public async Task FailedRegistrationCancelsADequeuedRunBeforeItsJobBodyExecutes()
    {
        ServiceCollection.AddNCronJob(builder =>
        {
            builder.WithMaxDegreeOfParallelism(1);
            builder.AddJob<DummyJob>(p => p.WithName("Registered"));
        });

        await StartNCronJob();

        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var queueManager = ServiceProvider.GetRequiredService<JobQueueManager>();
        var firstQueueAdded = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueRegistration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queueAdditions = 0;

        void DequeueFirstRunThenFail(string queueName)
        {
            queueAdditions++;
            if (queueAdditions == 1)
            {
                firstQueueAdded.TrySetResult(queueName);
                continueRegistration.Task.GetAwaiter().GetResult();
                return;
            }

            throw new InvalidOperationException("Scheduling failed.");
        }

        queueManager.QueueAdded += DequeueFirstRunThenFail;
        var registrationTask = Task.Run(() =>
        {
            var successful = runtimeJobRegistry.TryRegister(builder =>
            {
                builder.AddJob(
                    typeof(DummyJob),
                    p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Earlier"));
                builder.AddJob(
                    typeof(AnotherDummyJob),
                    p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Failure"));
            }, out var exception);

            return (successful, exception);
        }, CancellationToken);

        try
        {
            var queueName = await firstQueueAdded.Task.WaitAsync(CancellationToken);
            FakeTimer.Advance(TimeSpan.FromMinutes(1));
            await queueManager.WaitUntilEmptyAsync(queueName, CancellationToken);
        }
        finally
        {
            continueRegistration.TrySetResult();
        }

        (bool successful, Exception? exception) result;
        try
        {
            result = await registrationTask.WaitAsync(CancellationToken);
        }
        finally
        {
            queueManager.QueueAdded -= DequeueFirstRunThenFail;
        }

        var (successful, exception) = result;

        successful.ShouldBeFalse();
        exception.ShouldBeOfType<InvalidOperationException>();

        await WaitForJobState(ExecutionState.Cancelled, name: "Earlier");

        Storage.Entries.ShouldBeEmpty();
        Events.ShouldNotContain(e =>
            e.Name == "Earlier"
            && (e.State == ExecutionState.Initializing || e.State == ExecutionState.Running));
        jobRegistry.GetAllRootJobs().Select(job => job.CustomName).ShouldBe(["Registered"]);
        queueManager.GetAllJobQueueNames().ShouldBeEmpty();

        runtimeJobRegistry.TryRegister(
            builder => builder.AddJob(
                typeof(DummyJob),
                p => p.WithCronExpression(Cron.AtEveryMinute).WithName("AfterRollback")),
            out _).ShouldBeTrue();

        var followUpRun = await WaitForJobState(ExecutionState.Scheduled, name: "AfterRollback");
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForOrchestrationCompletion(followUpRun.CorrelationId);

        Storage.Entries.ShouldBe(["DummyJob - Parameter: "]);
    }

    [Fact]
    public void RegisteringDuplicateDuringRuntimeLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        var runtimeRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeRegistry.TryRegister(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
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

    private sealed class RollbackNotificationHandler : IJobNotificationHandler<DummyJob>
    {
        public Task HandleAsync(
            IJobExecutionContext context,
            Exception? exception,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
