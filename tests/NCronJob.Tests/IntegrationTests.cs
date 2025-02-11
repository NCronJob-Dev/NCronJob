using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public sealed class IntegrationTests : JobIntegrationBase
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task JobsShouldCancelOnCancellation()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var jobFinished = await DoNotWaitJustCancel(10);
        jobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        ServiceCollection.AddScoped<GuidGenerator>();
        ServiceCollection.AddNCronJob(n => n.AddJob<ScopedServiceJob>(
            p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("null")
                .And
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await Task.WhenAll(GetCompletionJobs(2, CancellationToken));

        Storage.Entries.Distinct().Count().ShouldBe(Storage.Entries.Count);
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAnInstantJobWithoutPreviousRegistration()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(token: CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindFirstJobDefinition(typeof(SimpleJob)).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAnInstantJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());

        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(token: CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task InstantJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>(token: CancellationToken);

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from AddNCronJob");
    }

    [Fact]
    public async Task InstantJobCanOverrideInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello from InstantJob", CancellationToken);

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from InstantJob");
    }

    [Fact]
    public async Task InstantJobShouldPassDownParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>());

        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello from InstantJob", CancellationToken);

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from InstantJob");
    }

    [Theory]
    [MemberData(nameof(InstantJobRunners))]
    public async Task InstantJobCanStartADisabledJob(Func<IInstantJobRegistry, string, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>((jo) => jo.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.DisableJob<ParameterJob>();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();
        var orchestrationId = instantJobRunner(instantJobRegistry, "Hello from InstantJob", CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[1].State.ShouldBe(ExecutionState.NotStarted);
        Events[2].State.ShouldBe(ExecutionState.Scheduled);
        Events[3].State.ShouldBe(ExecutionState.Cancelled);
        Events[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events[5].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[6].State.ShouldBe(ExecutionState.NotStarted);
        Events[7].State.ShouldBe(ExecutionState.Initializing);
        Events[8].State.ShouldBe(ExecutionState.Running);
        Events[9].State.ShouldBe(ExecutionState.Completing);
        Events[10].State.ShouldBe(ExecutionState.Completed);
        Events[11].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(12);
    }

    public static TheoryData<Func<IInstantJobRegistry, string, CancellationToken, Guid>> InstantJobRunners()
    {
        var t = new TheoryData<Func<IInstantJobRegistry, string, CancellationToken, Guid>>();
        t.Add((i, p, t) => i.RunInstantJob<ParameterJob>(p, t));
        t.Add((i, p, t) => i.ForceRunInstantJob<ParameterJob>(p, t));
        return t;
    }

    [Fact]
    public async Task CronJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from AddNCronJob");
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        FakeTimer.Advance(TimeSpan.FromSeconds(1));
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEverySecond)));

        await StartNCronJob();

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromSeconds(1));
        var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(
            p => p.WithCronExpression(Cron.AtEverySecond).And.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromSeconds(1));
        var jobFinished = await WaitForJobsOrTimeout(61, AdvanceTime);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockScheduler()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<LongRunningJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAScheduledJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        
        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(TimeSpan.FromMinutes(1), token: CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffset()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        
        var runDate = FakeTimer.GetUtcNow().AddMinutes(1);
        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(runDate, token: CancellationToken);

        await Task.Delay(10, CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffsetInThePast()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        
        var runDate = FakeTimer.GetUtcNow().AddDays(-1);

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(runDate, token: CancellationToken);

        await Task.Delay(10, CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeFalse();

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[1].State.ShouldBe(ExecutionState.NotStarted);
        Events[2].State.ShouldBe(ExecutionState.Expired);
        Events[3].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(4);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task WhileAwaitingJobTriggeringInstantJobShouldAnywayTriggerCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtMinute0)));

        await StartNCronJob(startMonitoringEvents: true);

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(instantOrchestrationId, stopMonitoringEvents: true);

        var scheduledOrchestrationId = Events[0].CorrelationId;

        var scheduledOrchestrationEvents = Events.WithOrchestrationId(scheduledOrchestrationId);
        var instantOrchestrationEvents = Events.WithOrchestrationId(instantOrchestrationId);

        scheduledOrchestrationEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        scheduledOrchestrationEvents[1].State.ShouldBe(ExecutionState.NotStarted);
        scheduledOrchestrationEvents[2].State.ShouldBe(ExecutionState.Scheduled);
        scheduledOrchestrationEvents[3].State.ShouldBe(ExecutionState.Cancelled);
        scheduledOrchestrationEvents[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        scheduledOrchestrationEvents.Count.ShouldBe(5);

        instantOrchestrationEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        instantOrchestrationEvents[1].State.ShouldBe(ExecutionState.NotStarted);
        instantOrchestrationEvents[2].State.ShouldBe(ExecutionState.Initializing);
        instantOrchestrationEvents[3].State.ShouldBe(ExecutionState.Running);
        instantOrchestrationEvents[4].State.ShouldBe(ExecutionState.Completing);
        instantOrchestrationEvents[5].State.ShouldBe(ExecutionState.Completed);
        instantOrchestrationEvents[6].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        instantOrchestrationEvents.Count.ShouldBe(7);

        // Scheduled orchestration should have started before the instant job related one...
        scheduledOrchestrationEvents[0].Timestamp.ShouldBeLessThan(instantOrchestrationEvents[0].Timestamp);

        // ...and cancelled before the initialization of the instant job related one.
        scheduledOrchestrationEvents[3].Timestamp.ShouldBeLessThan(instantOrchestrationEvents[2].Timestamp);
    }

    [Fact]
    public async Task MinimalJobApiCanBeUsedForTriggeringCronJobs()
    {
        ServiceCollection.AddNCronJob(async (ChannelWriter<object?> writer) =>
        {
            await writer.WriteAsync(null, CancellationToken);
        }, Cron.AtEveryMinute);

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ConcurrentJobConfigurationShouldBeRespected()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ShortRunningJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute).WithName("Job 1")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 2")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 3")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 4")));

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        Guid orchestrationId = Events[0].CorrelationId;

        await WaitUntilConditionIsMet(ASecondOrchestrationIsInitializing, stopMonitoringEvents: true);

        var runningJobs = Events.Where(e => e.State == ExecutionState.Initializing).ToList();

        runningJobs.Count.ShouldBe(2);
        runningJobs[0].CorrelationId.ShouldNotBe(runningJobs[1].CorrelationId);

        bool ASecondOrchestrationIsInitializing(ExecutionProgress @event)
        {
            return @event.State == ExecutionState.Initializing &&
                @event.CorrelationId != orchestrationId;
        }
    }

    [Fact]
    public async Task InstantJobHasHigherPriorityThanCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("CRON")));
        ServiceCollection.AddSingleton(_ => new ConcurrencySettings { MaxDegreeOfParallelism = 1 });

        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("INSTANT", CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var answer = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        answer.ShouldBe("INSTANT");
    }

    [Fact]
    public async Task TriggeringInstantJobWithoutRegisteringContinuesToWork()
    {
        ServiceCollection.AddNCronJob();
        
        await StartNCronJob();

        Action act = () => ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(token: CancellationToken);

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task ExecuteAnInstantJobDelegate()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        
        await StartNCronJob();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob(async (ChannelWriter<object> writer) =>
        {
            await writer.WriteAsync("Done", CancellationToken);
        }, CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AnonymousJobsCanBeExecutedMultipleTimes()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(async (ChannelWriter<object> writer, CancellationToken ct) =>
        {
            await Task.Delay(10, ct);
            await writer.WriteAsync(true, ct);
        }, Cron.AtEveryMinute));
        
        await StartNCronJob();
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForJobsOrTimeout(1);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, AdvanceTime);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task StaticAnonymousJobsCanBeExecutedMultipleTimes()
    {

        ServiceCollection.AddNCronJob(n => n.AddJob(JobMethods.WriteTrueStaticAsync, Cron.AtEveryMinute));

        await StartNCronJob();
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForJobsOrTimeout(1);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, AdvanceTime);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public void AddingJobsWithTheSameCustomNameLeadsToException()
    {
        Action act = () => ServiceCollection.AddNCronJob(
            n => n.AddJob(() => { }, Cron.AtEveryMinute, jobName: "Job1")
                .AddJob(() => { }, Cron.AtMinute0, jobName: "Job1"));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddJobsDynamicallyWhenNameIsDuplicatedLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(() => { }, Cron.AtEveryMinute, jobName: "Job1"));
        
        var runtimeRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeRegistry.TryRegister(n => n.AddJob(() => { }, Cron.AtEveryMinute, jobName: "Job1"), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task TwoJobsWithDifferentDefinitionLeadToTwoExecutions()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("1"))
            .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("2")));
        
        await StartNCronJob();

        var countJobs = ServiceProvider.GetRequiredService<JobRegistry>().GetAllCronJobs().Count;
        countJobs.ShouldBe(2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(2, TimeSpan.FromMilliseconds(250));
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    [SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    public async Task AddJobWithTypeAsParameterAddsJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(typeof(SimpleJob), p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AddingJobsAndDuringStartupAndRuntimeNotInOnlyOneCallOnlyOneExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>().AddJob<ShortRunningJob>());
        
        await StartNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        registry.TryRegister(n => n.AddJob<ShortRunningJob>(p => p.WithCronExpression("0 0 10 * *")));

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
        var anotherJobFinished = await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(500));
        anotherJobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task CallingAddNCronJobMultipleTimesWillRegisterAllJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ShortRunningJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(2);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public void RegisteringDuplicatedJobsLeadToAnExceptionWhenRegistration()
    {
        Action act = () =>
            ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .And
                .WithCronExpression(Cron.AtEveryMinute)));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void RegisteringDuplicateDuringRuntimeLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        
        var runtimeRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeRegistry.TryRegister(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    private static class JobMethods
    {
        public static async Task WriteTrueStaticAsync(ChannelWriter<object> writer, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            await writer.WriteAsync(true, ct);
        }
    }

    private sealed class GuidGenerator
    {
        public Guid NewGuid { get; } = Guid.NewGuid();
    }

    [SupportsConcurrency(2)]
    private sealed class SimpleJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            try
            {
                context.Output = "Job Completed";
                await Task.Delay(10, token);
                await writer.WriteAsync(context.Output, token);
            }
            catch (Exception ex)
            {
                await writer.WriteAsync(ex, token);
            }
        }
    }

    [SupportsConcurrency(2)]
    private sealed class ShortRunningJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            try
            {
                context.Output = "Job Completed";
                await writer.WriteAsync(context.Output, token);
                await Task.Delay(200, token);
            }
            catch (Exception ex)
            {
                await writer.WriteAsync(ex, token);
            }
        }
    }

    private sealed class LongRunningJob(TimeProvider timeProvider) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token) =>
            await Task.Delay(TimeSpan.FromSeconds(10), timeProvider, token);
    }

    private sealed class ScopedServiceJob(ChannelWriter<object> writer, Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Entries.Add(guidGenerator.NewGuid.ToString());
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class ParameterJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(context.Parameter!, token);
    }
}
