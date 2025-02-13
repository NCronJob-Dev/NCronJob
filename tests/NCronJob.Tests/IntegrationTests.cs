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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCompleted();
    }

    //[Fact]
    //public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task JobsShouldCancelOnCancellation()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    var jobFinished = await DoNotWaitJustCancel(10);
    //    jobFinished.ShouldBeFalse();
    //}

    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        ServiceCollection.AddScoped<GuidGenerator>();
        ServiceCollection.AddNCronJob(n => n.AddJob<ScopedServiceJob>(
            p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("null")
                .And
                .WithCronExpression(Cron.AtEveryMinute)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var firstOrchestrationId = events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitUntilConditionIsMet(events, ASecondOrchestrationHasCompleted);

        subscription.Dispose();

        Storage.Entries.Distinct().Count().ShouldBe(Storage.Entries.Count);
        Storage.Entries.Count.ShouldBe(2);

        ExecutionProgress? ASecondOrchestrationHasCompleted(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(e => e.CorrelationId != firstOrchestrationId
                && e.State == ExecutionState.OrchestrationCompleted);
        }
    }

    [Fact]
    public async Task ExecuteAnInstantJobWithoutPreviousRegistration()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJobAndExecuteInstantSimpleJob();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindFirstJobDefinition(typeof(SimpleJob)).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAnInstantJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());

        await StartNCronJobAndExecuteInstantSimpleJob();
    }

    //[Fact]
    //public async Task InstantJobShouldInheritInitiallyDefinedParameter()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(
    //        n => n.AddJob<ParameterJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>(token: CancellationToken);

    //    var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
    //    content.ShouldBe("Hello from AddNCronJob");
    //}

    //[Fact]
    //public async Task InstantJobCanOverrideInitiallyDefinedParameter()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(
    //        n => n.AddJob<ParameterJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello from InstantJob", CancellationToken);

    //    var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
    //    content.ShouldBe("Hello from InstantJob");
    //}

    //[Fact]
    //public async Task InstantJobShouldPassDownParameter()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(
    //        n => n.AddJob<ParameterJob>());

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello from InstantJob", CancellationToken);

    //    var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
    //    content.ShouldBe("Hello from InstantJob");
    //}

    [Theory]
    [MemberData(nameof(InstantJobRunners))]
    public async Task InstantJobCanStartADisabledJob(Func<IInstantJobRegistry, string, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>((jo) => jo.WithCronExpression(Cron.AtEveryMinute)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var scheduledOrchestrationId = events[0].CorrelationId;

        registry.DisableJob<ParameterJob>();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var instantOrchestrationId = instantJobRunner(instantJobRegistry, "Hello from InstantJob", CancellationToken);

        await WaitForOrchestrationCompletion(events, instantOrchestrationId);

        subscription.Dispose();

        var scheduledOrchestrationEvents = events.FilterByOrchestrationId(scheduledOrchestrationId);
        scheduledOrchestrationEvents.ShouldBeScheduledThenCancelled();

        var instantOrchestrationEvents = events.FilterByOrchestrationId(instantOrchestrationId);
        instantOrchestrationEvents.ShouldBeInstantThenCompleted();
    }

    public static TheoryData<Func<IInstantJobRegistry, string, CancellationToken, Guid>> InstantJobRunners()
    {
        var t = new TheoryData<Func<IInstantJobRegistry, string, CancellationToken, Guid>>();
        t.Add((i, p, t) => i.RunInstantJob<ParameterJob>(p, t));
        t.Add((i, p, t) => i.ForceRunInstantJob<ParameterJob>(p, t));
        return t;
    }

    //[Fact]
    //public async Task CronJobShouldInheritInitiallyDefinedParameter()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(
    //        n => n.AddJob<ParameterJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("Hello from AddNCronJob")));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));

    //    var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
    //    content.ShouldBe("Hello from AddNCronJob");
    //}

    //[Fact]
    //public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    //{
    //    // TODO: To be migrated

    //    FakeTimer.Advance(TimeSpan.FromSeconds(1));
    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEverySecond)));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromSeconds(1));
    //    var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(
    //        p => p.WithCronExpression(Cron.AtEverySecond).And.WithCronExpression(Cron.AtEveryMinute)));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromSeconds(1));
    //    var jobFinished = await WaitForJobsOrTimeout(61, AdvanceTime);
    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task LongRunningJobShouldNotBlockScheduler()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n
    //            .AddJob<LongRunningJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
    //            .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1);
    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task ExecuteAScheduledJob()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(TimeSpan.FromMinutes(1), token: CancellationToken);

    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1);
    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task ExecuteAScheduledJobWithDateTimeOffset()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());

    //    var runDate = FakeTimer.GetUtcNow().AddMinutes(1);
    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(runDate, token: CancellationToken);

    //    await Task.Delay(10, CancellationToken);
    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1);
    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task ExecuteAScheduledJobWithDateTimeOffsetInThePast()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());

    //    var runDate = FakeTimer.GetUtcNow().AddDays(-1);

    //    (var subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(runDate, token: CancellationToken);

    //    await Task.Delay(10, CancellationToken);
    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1);
    //    jobFinished.ShouldBeFalse();

    //    await WaitForOrchestrationCompletion(events, orchestrationId);

    //    subscription.Dispose();

    //    events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
    //    events[1].State.ShouldBe(ExecutionState.NotStarted);
    //    events[2].State.ShouldBe(ExecutionState.Expired);
    //    events[3].State.ShouldBe(ExecutionState.OrchestrationCompleted);
    //    events.Count.ShouldBe(4);
    //    events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    //}

    [Fact]
    public async Task WhileAwaitingJobTriggeringInstantJobShouldAnywayTriggerCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtMinute0)));

        (var subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(events, instantOrchestrationId);

        subscription.Dispose();

        var scheduledOrchestrationId = events[0].CorrelationId;

        var scheduledOrchestrationEvents = events.FilterByOrchestrationId(scheduledOrchestrationId);
        scheduledOrchestrationEvents.ShouldBeScheduledThenCancelled();

        var instantOrchestrationEvents = events.FilterByOrchestrationId(instantOrchestrationId);
        instantOrchestrationEvents.ShouldBeInstantThenCompleted();

        // Scheduled orchestration should have started before the instant job related one...
        scheduledOrchestrationEvents[0].Timestamp.ShouldBeLessThan(instantOrchestrationEvents[0].Timestamp);

        // ...and cancelled before the initialization of the instant job related one.
        scheduledOrchestrationEvents[3].Timestamp.ShouldBeLessThan(instantOrchestrationEvents[2].Timestamp);
    }

    //[Fact]
    //public async Task MinimalJobApiCanBeUsedForTriggeringCronJobs()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(async (ChannelWriter<object?> writer) =>
    //    {
    //        await writer.WriteAsync(null, CancellationToken);
    //    }, Cron.AtEveryMinute);

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1);
    //    jobFinished.ShouldBeTrue();
    //}

    [Fact]
    public async Task ConcurrentJobConfigurationShouldBeRespected()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ShortRunningJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute).WithName("Job 1")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 2")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 3")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 4")));

        (var subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = events[0].CorrelationId;

        await WaitUntilConditionIsMet(events, ASecondOrchestrationIsInitializing);

        subscription.Dispose();

        var runningJobs = events.Where(e => e.State == ExecutionState.Initializing).ToList();

        runningJobs.Count.ShouldBe(2);
        runningJobs[0].CorrelationId.ShouldNotBe(runningJobs[1].CorrelationId);

        ExecutionProgress? ASecondOrchestrationIsInitializing(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(e => e.State == ExecutionState.Initializing &&
                e.CorrelationId != orchestrationId);
        }
    }

    //[Fact]
    //public async Task InstantJobHasHigherPriorityThanCronJob()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("CRON")));
    //    ServiceCollection.AddSingleton(_ => new ConcurrencySettings { MaxDegreeOfParallelism = 1 });

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("INSTANT", CancellationToken);
    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));

    //    var answer = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
    //    answer.ShouldBe("INSTANT");
    //}

    [Fact]
    public async Task TriggeringInstantJobWithoutRegisteringContinuesToWork()
    {
        ServiceCollection.AddNCronJob();

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Action act = () => ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(token: CancellationToken);

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task ExecuteAnInstantJobDelegate()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());

        (var subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob((Storage storage) =>
        {
            storage.Add("Done");
        }, CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenCompleted();

        Storage.Entries[0].ShouldBe("Done");
    }

    //[Fact]
    //public async Task AnonymousJobsCanBeExecutedMultipleTimes()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob(async (ChannelWriter<object> writer, CancellationToken ct) =>
    //    {
    //        await Task.Delay(10, ct);
    //        await writer.WriteAsync(true, ct);
    //    }, Cron.AtEveryMinute));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    await WaitForJobsOrTimeout(1);

    //    void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1, AdvanceTime);
    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task StaticAnonymousJobsCanBeExecutedMultipleTimes()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob(JobMethods.WriteTrueStaticAsync, Cron.AtEveryMinute));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    await WaitForJobsOrTimeout(1);

    //    void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1, AdvanceTime);
    //    jobFinished.ShouldBeTrue();
    //}

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

    //[Fact]
    //public async Task TwoJobsWithDifferentDefinitionLeadToTwoExecutions()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n
    //        .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("1"))
    //        .AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("2")));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    var countJobs = ServiceProvider.GetRequiredService<JobRegistry>().GetAllCronJobs().Count;
    //    countJobs.ShouldBe(2);

    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(2, TimeSpan.FromMilliseconds(250));
    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //[SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    //public async Task AddJobWithTypeAsParameterAddsJobs()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob(typeof(SimpleJob), p => p.WithCronExpression(Cron.AtEveryMinute)));

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1);
    //    jobFinished.ShouldBeTrue();
    //}

    //[Fact]
    //public async Task AddingJobsAndDuringStartupAndRuntimeNotInOnlyOneCallOnlyOneExecution()
    //{
    //    // TODO: To be migrated

    //    ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>().AddJob<ShortRunningJob>());

    //    await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
    //    var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

    //    registry.TryRegister(n => n.AddJob<SimpleJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
    //    registry.TryRegister(n => n.AddJob<ShortRunningJob>(p => p.WithCronExpression("0 0 10 * *")));

    //    FakeTimer.Advance(TimeSpan.FromMinutes(1));
    //    var jobFinished = await WaitForJobsOrTimeout(1);
    //    jobFinished.ShouldBeTrue();
    //    var anotherJobFinished = await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(500));
    //    anotherJobFinished.ShouldBeFalse();
    //}

    [Fact]
    public async Task CallingAddNCronJobMultipleTimesWillRegisterAllJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        ServiceCollection.AddNCronJob(n => n.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        (var subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var firstOrchestrationId = events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitUntilConditionIsMet(events, ASecondOrchestrationHasCompleted);

        subscription.Dispose();

        Storage.Entries.ShouldContain("DummyJob");
        Storage.Entries.ShouldContain("AnotherDummyJob");
        Storage.Entries.Count.ShouldBe(2);

        ExecutionProgress? ASecondOrchestrationHasCompleted(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(e => e.CorrelationId != firstOrchestrationId
                && e.State == ExecutionState.OrchestrationCompleted);
        }
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

    private async Task StartNCronJobAndExecuteInstantSimpleJob()
    {
        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenCompleted();
    }

    private static class JobMethods
    {
        public static async Task WriteTrueStaticAsync(Storage storage, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            storage.Add("true");
        }
    }

    private sealed class GuidGenerator
    {
        public Guid NewGuid { get; } = Guid.NewGuid();
    }

    [SupportsConcurrency(2)]
    private sealed class SimpleJob(Storage storage) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            try
            {
                context.Output = "Job Completed";
                await Task.Delay(10, token);
                storage.Add(context.Output.ToString()!);
            }
            catch (Exception ex)
            {
                storage.Add(ex.GetType().Name!);
            }
        }
    }

    [SupportsConcurrency(2)]
    private sealed class ShortRunningJob(Storage storage) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            try
            {
                context.Output = "Job Completed";
                storage.Add(context.Output.ToString()!);
                await Task.Delay(200, token);
            }
            catch (Exception ex)
            {
                storage.Add(ex.GetType().Name!);
            }
        }
    }

    private sealed class LongRunningJob(TimeProvider timeProvider) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token) =>
            await Task.Delay(TimeSpan.FromSeconds(10), timeProvider, token);
    }

    private sealed class ScopedServiceJob(Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Entries.Add(guidGenerator.NewGuid.ToString());
            return Task.CompletedTask;
        }
    }

    private sealed class ParameterJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(context.Parameter!.ToString()!);
            return Task.CompletedTask;
        }
    }
}
