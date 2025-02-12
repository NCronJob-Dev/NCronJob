using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : JobIntegrationBase
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCompleted();
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));

        AdvanceTime();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            events,
            ExecutionState.OrchestrationCompleted,
            10,
            AdvanceTime);

        subscription.Dispose();

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(events, e));

        Storage.Entries.ShouldAllBe(e => e == "DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(10);
    }

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

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(events, ExecutionState.Completed, 2);

        subscription.Dispose();

        Storage.Entries.Distinct().Count().ShouldBe(Storage.Entries.Count);
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAnInstantJobWithoutPreviousRegistration()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJobAndExecuteInstantSimpleJob();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindFirstJobDefinition(typeof(DummyJob)).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAnInstantJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        await StartNCronJobAndExecuteInstantSimpleJob();
    }

    [Fact]
    public async Task InstantJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from AddNCronJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InstantJobCanOverrideInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("Hello from InstantJob", CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from InstantJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InstantJobShouldPassDownParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>());

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("Hello from InstantJob", CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from InstantJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InstantJobRunners))]
    public async Task InstantJobCanStartADisabledJob(Func<IInstantJobRegistry, string, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>((jo) => jo.WithCronExpression(Cron.AtEveryMinute)));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var scheduledOrchestrationId = events[0].CorrelationId;

        registry.DisableJob<DummyJob>();

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
        t.Add((i, p, t) => i.RunInstantJob<DummyJob>(p, t));
        t.Add((i, p, t) => i.ForceRunInstantJob<DummyJob>(p, t));
        return t;
    }

    [Fact]
    public async Task CronJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("Hello from AddNCronJob")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from AddNCronJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEverySecond)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(events, ExecutionState.OrchestrationCompleted, 10);

        subscription.Dispose();

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(events, e));

        Storage.Entries.ShouldAllBe(e => e == "DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(
            p => p.WithCronExpression(Cron.AtEverySecond).WithParameter("Second")
                .And.WithCronExpression(Cron.AtEveryMinute).WithParameter("Minute")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromSeconds(1));

        AdvanceTime();

        await WaitForNthOrchestrationState(events, ExecutionState.OrchestrationCompleted, 61, AdvanceTime);

        subscription.Dispose();

        var minuteJobOutputs = Storage.Entries.Where(e => e == "DummyJob - Parameter: Minute");

        minuteJobOutputs.Count().ShouldBe(1);
        Storage.Entries.Except(minuteJobOutputs).ShouldAllBe(e => e == "DummyJob - Parameter: Second");
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockScheduler()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<LongRunningJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(events, ExecutionState.Running, 2);
        await WaitForNthOrchestrationState(events, ExecutionState.OrchestrationCompleted, 1);

        subscription.Dispose();

        Storage.Entries.ShouldContain("DummyJob - Parameter: ");
        Storage.Entries.ShouldContain("Running LongRunningJob");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAScheduledJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<DummyJob>(TimeSpan.FromMinutes(1), token: CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffset()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        var runDate = FakeTimer.GetUtcNow().AddMinutes(1);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<DummyJob>(runDate, token: CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffsetInThePast()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        var runDate = FakeTimer.GetUtcNow().AddDays(-1);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<DummyJob>(runDate, token: CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenExpired();

        Storage.Entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task WhileAwaitingJobTriggeringInstantJobShouldAnywayTriggerCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute0)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

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

    [Fact]
    public async Task MinimalJobApiCanBeUsedForTriggeringCronJobs()
    {
        ServiceCollection.AddNCronJob((Storage storage) =>
        {
            storage.Add("Done");
        }, Cron.AtEveryMinute);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredOrchestrationEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries[0].ShouldBe("Done");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentJobConfigurationShouldBeRespected()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ConcurrentJob>(p => p
            .WithCronExpression(Cron.AtEveryMinute).WithName("Job 1")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 2")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 3")
            .And.WithCronExpression(Cron.AtEveryMinute).WithName("Job 4")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var initializingOrchestrationEvents = await WaitForNthOrchestrationState(events, ExecutionState.Initializing, 2);

        subscription.Dispose();

        initializingOrchestrationEvents.Count.ShouldBe(2);
        initializingOrchestrationEvents[0].CorrelationId.ShouldNotBe(initializingOrchestrationEvents[1].CorrelationId);
    }

    [Fact]
    public async Task InstantJobHasHigherPriorityThanCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("CRON")));
        ServiceCollection.AddSingleton(new ConcurrencySettings { MaxDegreeOfParallelism = 1 });

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var scheduledOrchestrationId = events[0].CorrelationId;

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("INSTANT", CancellationToken);

        await WaitForOrchestrationCompletion(events, instantOrchestrationId);

        subscription.Dispose();

        var scheduledOrchestrationEvents = events.FilterByOrchestrationId(scheduledOrchestrationId);
        scheduledOrchestrationEvents.ShouldBeScheduledThenCancelled();

        var instantOrchestrationEvents = events.FilterByOrchestrationId(instantOrchestrationId);
        instantOrchestrationEvents.ShouldBeInstantThenCompleted();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: INSTANT");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TriggeringInstantJobWithoutRegisteringContinuesToWork()
    {
        ServiceCollection.AddNCronJob();

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Action act = () => ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task ExecuteAnInstantJobDelegate()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

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

    [Fact]
    public async Task AnonymousJobsCanBeExecutedMultipleTimes()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob((Storage storage) =>
        {
            storage.Add("true");
        }, Cron.AtEveryMinute));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));

        AdvanceTime();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            events,
            ExecutionState.OrchestrationCompleted,
            10,
            AdvanceTime);

        subscription.Dispose();

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(events, e));

        Storage.Entries.ShouldAllBe(e => e == "true");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task StaticAnonymousJobsCanBeExecutedMultipleTimes()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(JobMethods.WriteTrueStaticAsync, Cron.AtEveryMinute));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));

        AdvanceTime();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            events,
            ExecutionState.OrchestrationCompleted,
            10,
            AdvanceTime);

        subscription.Dispose();

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(events, e));

        Storage.Entries.ShouldAllBe(e => e == "true");
        Storage.Entries.Count.ShouldBe(10);
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
            .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("1"))
            .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("2")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var countJobs = ServiceProvider.GetRequiredService<JobRegistry>().GetAllCronJobs().Count;
        countJobs.ShouldBe(2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(events, ExecutionState.OrchestrationCompleted, 2);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: 1");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: 2");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    [SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    public async Task AddJobWithTypeAsParameterAddsJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(typeof(DummyJob), p => p.WithCronExpression(Cron.AtEveryMinute)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddingRuntimeJobsWillNotCauseDuplicatedExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>().AddJob<AnotherDummyJob>());

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        events.Count.ShouldBe(0);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        registry.TryRegister(n => n.AddJob<AnotherDummyJob>(p => p.WithCronExpression("0 0 10 * *")));

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(events, ExecutionState.OrchestrationCompleted, 1);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CallingAddNCronJobMultipleTimesWillRegisterAllJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        ServiceCollection.AddNCronJob(n => n.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(events, ExecutionState.OrchestrationCompleted, 2);

        subscription.Dispose();

        Storage.Entries.ShouldContain("DummyJob - Parameter: ");
        Storage.Entries.ShouldContain("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public void RegisteringDuplicatedJobsLeadToAnExceptionWhenRegistration()
    {
        Action act = () =>
            ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .And
                .WithCronExpression(Cron.AtEveryMinute)));

        act.ShouldThrow<InvalidOperationException>();
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

    private async Task StartNCronJobAndExecuteInstantSimpleJob()
    {
        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenCompleted();
    }

    private static bool OrchestrationIsScheduledThenCompleted(IList<ExecutionProgress> events, ExecutionProgress executionProgress)
    {
        var filteredEvents = events.FilterByOrchestrationId(executionProgress.CorrelationId);
        filteredEvents.ShouldBeScheduledThenCompleted();
        return true;
    }

    private static class JobMethods
    {
        public static Task WriteTrueStaticAsync(Storage storage)
        {
            storage.Add("true");

            return Task.CompletedTask;
        }
    }

    private sealed class GuidGenerator
    {
        public Guid NewGuid { get; } = Guid.NewGuid();
    }

    [SupportsConcurrency(2)]
    private sealed class ConcurrentJob : DummyJob
    {
        public ConcurrentJob(Storage storage)
            : base(storage)
        {
        }
    }

    private sealed class ScopedServiceJob(Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(guidGenerator.NewGuid.ToString());
            return Task.CompletedTask;
        }
    }
}
