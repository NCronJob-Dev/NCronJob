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
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true, CancellationToken), Cron.AtEveryMinute), out _);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task MultipleDynamicallyAddedJobsAreExecuted()
    {
        ServiceCollection.AddNCronJob();
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

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
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob("Job");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(200));
        jobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task DoesNotCringeWhenRemovingNonExistingJobs()
    {
        ServiceCollection.AddNCronJob();

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = provider.GetRequiredService<JobRegistry>();
        Assert.Empty(jobRegistry.GetAllJobs());

        registry.RemoveJob("Nope");
        registry.RemoveJob<SimpleJob>();
    }

    [Fact]
    public async Task CanRemoveByJobType()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

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

        var provider = CreateServiceProvider();

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(provider);

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = provider.GetRequiredService<JobRegistry>();
        Assert.Equal(2, jobRegistry.FindAllJobDefinition(typeof(SimpleJob)).Count);

        List<Guid> orchestrationIds = events.Select(e => e.CorrelationId).Distinct().ToList();
        Assert.Equal(2, orchestrationIds.Count);

        foreach (var orchestrationId in orchestrationIds)
        {
            List<ExecutionProgress> orchestrationEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();
            Assert.Equal(ExecutionState.OrchestrationStarted, orchestrationEvents[0].State);
            Assert.Equal(ExecutionState.NotStarted, orchestrationEvents[1].State);
            Assert.Equal(ExecutionState.Scheduled, orchestrationEvents[2].State);
            Assert.Equal(3, orchestrationEvents.Count);
        }

        registry.RemoveJob<SimpleJob>();

        Assert.Empty(jobRegistry.FindAllJobDefinition(typeof(SimpleJob)));

        subscription.Dispose();

        foreach (var orchestrationId in orchestrationIds)
        {
            List<ExecutionProgress> orchestrationEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();
            Assert.Equal(ExecutionState.OrchestrationStarted, orchestrationEvents[0].State);
            Assert.Equal(ExecutionState.NotStarted, orchestrationEvents[1].State);
            Assert.Equal(ExecutionState.Scheduled, orchestrationEvents[2].State);
            Assert.Equal(ExecutionState.Cancelled, orchestrationEvents[3].State);
            Assert.Equal(ExecutionState.OrchestrationCompleted, orchestrationEvents[4].State);
            Assert.Equal(5, orchestrationEvents.Count);
        }

        Assert.Equal(10, events.Count);
    }

    [Fact]
    public async Task CanUpdateScheduleOfAJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression("0 0 * * *").WithName("JobName")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        registry.UpdateSchedule("JobName", Cron.AtEveryMinute);

        var jobRegistry = provider.GetRequiredService<JobRegistry>();
        var jobDefinition = jobRegistry.GetAllJobs().Single();

        Assert.Equal(Cron.AtEveryMinute, jobDefinition.UserDefinedCronExpression);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldThrowAnExceptionWhenJobIsNotFoundAndTryingToUpdateSchedule()
    {
        ServiceCollection.AddNCronJob();
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.UpdateSchedule("JobName", Cron.AtEveryMinute));
    }

    [Fact]
    public async Task ShouldRetrieveScheduleForCronJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEvery2ndMinute).WithName("JobName")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = registry.TryGetSchedule("JobName", out var cronExpression, out var timeZoneInfo);

        successful.ShouldBeTrue();
        cronExpression.ShouldBe(Cron.AtEvery2ndMinute);
        timeZoneInfo.ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public async Task ShouldReturnFalseIfGivenJobWasNotFound()
    {
        ServiceCollection.AddNCronJob();
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

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
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

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
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

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
        var provider = CreateServiceProvider();
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();
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
        var provider = CreateServiceProvider();
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();
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
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

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

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = provider.GetRequiredService<JobRegistry>();
        var jobs = jobRegistry.FindAllJobDefinition(typeof(SimpleJob));
        Assert.Equal(2, jobs.Count);

        Assert.All(jobs, j =>
        {
            if (j.CronExpression is null)
            {
                return;
            }

            Assert.NotEqual(RuntimeJobRegistry.TheThirtyFirstOfFebruary, j.CronExpression);
        });

        registry.DisableJob<SimpleJob>();

        jobs = jobRegistry.FindAllJobDefinition(typeof(SimpleJob));
        Assert.Equal(2, jobs.Count);

        Assert.All(jobs, j =>
        {
            Assert.Equal(RuntimeJobRegistry.TheThirtyFirstOfFebruary, j.CronExpression);
        });

        registry.EnableJob<SimpleJob>();

        jobs = jobRegistry.FindAllJobDefinition(typeof(SimpleJob));
        Assert.Equal(2, jobs.Count);

        Assert.Single(jobs, j => j.CronExpression is null);
        Assert.Single(jobs, j => j.CronExpression is not null && j.CronExpression.ToString() == Cron.AtMinute2);
    }

    [Fact]
    public async Task ShouldThrowAnExceptionWhenJobIsNotFoundAndTryingToDisable()
    {
        ServiceCollection.AddNCronJob();
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.DisableJob("JobName"));
    }

    [Fact]
    public async Task ShouldEnableJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();
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
        var runtimeJobRegistry = CreateServiceProvider().GetRequiredService<IRuntimeJobRegistry>();

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
        var runtimeJobRegistry = CreateServiceProvider().GetRequiredService<IRuntimeJobRegistry>();
        runtimeJobRegistry.TryRegister(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void TryRegisterShouldIndicateSuccess()
    {
        ServiceCollection.AddNCronJob();
        var runtimeJobRegistry = CreateServiceProvider().GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeTrue();
        exception.ShouldBeNull();
    }

    private sealed class SimpleJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token) => await writer.WriteAsync(context.Parameter ?? string.Empty, token);
    }
}
