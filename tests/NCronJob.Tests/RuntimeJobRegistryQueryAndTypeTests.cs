using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobRegistryQueryAndTypeTests : JobIntegrationBase
{
    [Fact]
    public void TryGetNextOccurrenceUsesCurrentUtcTimeAndConfiguredTimeZone()
    {
        FakeTimer.SetUtcNow(new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero));
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job
            .WithName("Job")
            .WithCronExpression("0 8 * * *", timeZone)));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeTrue();
        nextRun.ShouldBe(new DateTimeOffset(2024, 1, 1, 16, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsFalseForUnknownJob()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Unknown", out var nextRun).ShouldBeFalse();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsFalseForUnscheduledJob()
    {
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job.WithName("Job")));
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeFalse();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsFalseForDisabledJob()
    {
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job
            .WithName("Job")
            .WithCronExpression(Cron.AtEveryMinute)));
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.DisableJob("Job");

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeFalse();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsTrueWithNullWhenScheduleHasNoFutureOccurrence()
    {
        FakeTimer.SetUtcNow(new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero));
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job
            .WithName("Job")
            .WithCronExpression(Cron.AtEveryMinute)));
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeTrue();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void EnableJobByTypeThrowsWhenNoRootJobMatches()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var exception = Should.Throw<InvalidOperationException>(() => registry.EnableJob(typeof(DummyJob)));

        exception.Message.ShouldBe($"Root job with type '{typeof(DummyJob)}' not found.");
    }

    [Fact]
    public void DisableJobByTypeThrowsWhenNoRootJobMatches()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var exception = Should.Throw<InvalidOperationException>(() => registry.DisableJob(typeof(DummyJob)));

        exception.Message.ShouldBe($"Root job with type '{typeof(DummyJob)}' not found.");
    }

    [Fact]
    public void EnableAndDisableJobByTypeValidateNull()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<ArgumentNullException>(() => registry.EnableJob((Type)null!));
        Should.Throw<ArgumentNullException>(() => registry.DisableJob((Type)null!));
    }

    [Fact]
    public void EnableAndDisableJobByTypeRemainIdempotentForMultipleRegistrations()
    {
        ServiceCollection.AddNCronJob(options =>
        {
            options.AddJob<DummyJob>(job => job.WithName("First").WithCronExpression(Cron.AtEveryMinute));
            options.AddJob<DummyJob>(job => job.WithName("Second").WithCronExpression(Cron.AtMinute2));
        });
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobs = ServiceProvider.GetRequiredService<JobRegistry>().FindAllRootJobDefinition(typeof(DummyJob));

        registry.DisableJob(typeof(DummyJob));
        registry.DisableJob(typeof(DummyJob));
        jobs.ShouldAllBe(job => !job.IsEnabled);

        registry.EnableJob(typeof(DummyJob));
        registry.EnableJob(typeof(DummyJob));
        jobs.ShouldAllBe(job => job.IsEnabled);
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

        ServiceProvider.GetRequiredService<CronRunScheduler>().ScheduleNextRun(orphan);

        ServiceProvider.GetRequiredService<JobQueueManager>().GetAllJobQueueNames().ShouldBeEmpty();
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
}
