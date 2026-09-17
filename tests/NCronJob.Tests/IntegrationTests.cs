using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class IntegrationTests : JobIntegrationBase
{
    private static readonly Delegate JobDelegate = () => { };

    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCompleted<DummyJob>();
    }

    [Fact]
    public async Task CronJobScheduledWithAMacroShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression("@hourly").WithName("Hourly")));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        registry.TryGetSchedule("Hourly", out var cronExpression, out _).ShouldBeTrue();
        cronExpression.ShouldBe("@hourly");

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromHours(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).ShouldBeScheduledThenCompleted<DummyJob>("Hourly");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ThrowingProgressCallbackDoesNotBreakJobExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        using var throwingSubscription = ServiceProvider
            .GetRequiredService<IJobExecutionProgressReporter>()
            .Register(_ => throw new InvalidOperationException("Faulty subscriber"));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).ShouldBeScheduledThenCompleted<DummyJob>();
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeAndWaitForStateCount(
            TimeSpan.FromMinutes(1),
            advances: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted<DummyJob>(Events, e));

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

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.Completed,
            2);

        Storage.Entries.Distinct().Count().ShouldBe(Storage.Entries.Count);
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAnInstantTypedJobWithoutPreviousRegistration()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJobAndExecuteInstantTypedJob();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindFirstRootJobDefinition(typeof(DummyJob)).ShouldBeNull();
    }

    [Fact]
    public async Task ForceExecuteAnUntypedInstantJobWithoutPreviousRegistration()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJobAndExecuteInstantUntypedJob((ijr, token) => IInstantJobRegistryExtensions.ForceRunInstantJob(ijr, untypedJob, token));

        await StartNCronJobAndExecuteInstantUntypedJob((ijr, token) => ijr.ForceRunScheduledJob(untypedJob, TimeSpan.Zero, token));
    }

    [Fact]
    public async Task ExecuteAnInstantTypedJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        await StartNCronJobAndExecuteInstantTypedJob();
    }

    [Fact]
    public async Task InstantJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from AddNCronJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InstantJobWithoutArgumentsShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();
        var registry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        // Arguments are deliberately omitted to guard against ambiguous overloads (#376)
#pragma warning disable xUnit1051, CA2263
        var orchestrationIds = new[]
        {
            registry.RunInstantJob<DummyJob>(),
            registry.RunInstantJob(typeof(DummyJob)),
            registry.ForceRunInstantJob<DummyJob>(),
            registry.ForceRunInstantJob(typeof(DummyJob)),
            registry.RunScheduledJob<DummyJob>(TimeSpan.Zero),
            registry.ForceRunScheduledJob<DummyJob>(TimeSpan.Zero),
            registry.RunScheduledJob(typeof(DummyJob), TimeSpan.Zero),
        };
#pragma warning restore xUnit1051, CA2263

        foreach (var orchestrationId in orchestrationIds)
        {
            await WaitForOrchestrationCompletion(orchestrationId);
        }

        Storage.Entries.Count.ShouldBe(orchestrationIds.Length);
        Storage.Entries.ShouldAllBe(e => e == "DummyJob - Parameter: Hello from AddNCronJob");
    }

    [Fact]
    public async Task InstantJobCanOverrideInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("Hello from InstantJob", CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from InstantJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InstantJobCanExplicitlyOverrideConfiguredParameterWithNull()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never).WithParameter("Configured")));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .RunInstantJob<DummyJob>(parameter: null, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries.ShouldBe(["DummyJob - Parameter: "]);
    }

    [Fact]
    public async Task InstantJobShouldPassDownParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>());

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("Hello from InstantJob", CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from InstantJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InstantJobRunners))]
    public async Task InstantJobCanStartADisabledJob(
        Func<IInstantJobRegistry, TimeProvider, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>((jo) => jo.WithCronExpression(Cron.AtEveryMinute)));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        await StartNCronJob();

        var scheduledOrchestrationId = Events[0].CorrelationId;

        registry.DisableJob<DummyJob>();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var instantOrchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "Hello from InstantJob", CancellationToken);

        await WaitForOrchestrationCompletion(instantOrchestrationId);

        var scheduledOrchestrationEvents = Events.FilterByOrchestrationId(scheduledOrchestrationId);
        scheduledOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();

        var instantOrchestrationEvents = Events.FilterByOrchestrationId(instantOrchestrationId);
        instantOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();
    }

    [Theory]
    [MemberData(nameof(InstantJobRunners))]
    public async Task ShouldThrowRuntimeExceptionWhenTriggeringThroughTheInstantJobRegistryAnAmbiguousTypeReference(
        Func<IInstantJobRegistry, TimeProvider, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.AtMinute5))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>());
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        Action act = () => instantJobRunner(instantJobRegistry, FakeTimer, "Hello from InstantJob", CancellationToken);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Ambiguous job reference for type 'DummyJob' detected.");
    }

    public static TheoryData<Func<IInstantJobRegistry, TimeProvider, object?, CancellationToken, Guid>> InstantJobRunners()
    {
        var t = new TheoryData<Func<IInstantJobRegistry, TimeProvider, object?, CancellationToken, Guid>>();
        t.Add((i, f, p, t) => i.RunInstantJob<DummyJob>(p, t));
        t.Add((i, f, p, t) => i.RunScheduledJob<DummyJob>(f.GetUtcNow(), p, t));
        t.Add((i, f, p, t) => i.ForceRunInstantJob<DummyJob>(p, t));
        t.Add((i, f, p, t) => i.ForceRunScheduledJob<DummyJob>(TimeSpan.Zero, p, t));
        return t;
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task NamedInstantJobsCanExplicitlyOverrideConfiguredParameterWithNull(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(s => s.WithName("good").WithParameter("good_param"))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.Never).WithParameter("bad_param"))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>());
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", null, CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task CanDisambiguateSimarlyTypedJobsThroughNamesWhileOverridingParameters(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(s => s.WithName("good").WithParameter("good_param"))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.Never).WithParameter("bad_param"))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>());
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", "overriden_param", CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: overriden_param");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task CanTrigerDynamicJobsThroughNames(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(
                untypedJob,
                Cron.Never,
                jobName: "good");
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", null, CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("Done - Parameter : ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task CanTrigerDynamicJobsThroughNamesWhilePassingParameters(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(
                untypedJob,
                Cron.Never,
                jobName: "good");
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", "param", CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("Done - Parameter : param");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task ShouldThrowRuntimeExceptionWhenTriggeringThroughTheInstantJobRegistryAnUnregisteredNamedJob(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        Action act = () => instantJobRunner(instantJobRegistry, FakeTimer, "good", "param", CancellationToken);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Job with name 'good' not found.");
    }

    public static TheoryData<Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid>> InstantNamedJobRunners()
    {
        var t = new TheoryData<Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid>>();
        t.Add((i, f, n, p, t) => i.RunInstantJob(n, p, t));
        t.Add((i, f, n, p, t) => i.RunScheduledJob(n, f.GetUtcNow(), p, t));
        t.Add((i, f, n, p, t) => i.ForceRunInstantJob(n, p, t));
        t.Add((i, f, n, p, t) => i.ForceRunScheduledJob(n, TimeSpan.Zero, p, t));
        return t;
    }

    [Fact]
    public async Task CronJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("Hello from AddNCronJob")));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: Hello from AddNCronJob");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEverySecond)));

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeAndWaitForStateCount(
            TimeSpan.FromSeconds(1),
            advances: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted<DummyJob>(Events, e));

        Storage.Entries.ShouldAllBe(e => e == "DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(
            p => p.WithCronExpression(Cron.AtEverySecond).WithParameter("Second")
                .And.WithCronExpression(Cron.AtEveryMinute).WithParameter("Minute")));

        await StartNCronJob();

        await AdvanceTimeAndWaitForStateCount(
            TimeSpan.FromSeconds(1),
            advances: 61,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 61);

        var minuteJobOutputs = Storage.Entries.Where(e => e == "DummyJob - Parameter: Minute");

        minuteJobOutputs.Count().ShouldBe(1);
        Storage.Entries.Except(minuteJobOutputs).ShouldAllBe(e => e == "DummyJob - Parameter: Second");
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockScheduler()
    {
        var signal = new LongRunningJobSignal();
        ServiceCollection.AddSingleton(signal);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<LongRunningJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(ExecutionState.Running, 2);
        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            1);
        await signal.Started.Task.WaitAsync(CancellationToken);

        Storage.Entries.ShouldContain("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAScheduledJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<DummyJob>(TimeSpan.FromMinutes(1), token: CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffset()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        var runDate = FakeTimer.GetUtcNow().AddMinutes(1);

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<DummyJob>(runDate, token: CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffsetInThePast()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        var runDate = FakeTimer.GetUtcNow().AddDays(-1);

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<DummyJob>(runDate, token: CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeInstantThenExpired<DummyJob>();

        Storage.Entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task WhileAwaitingJobTriggeringInstantJobShouldAnywayTriggerCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute0)));

        await StartNCronJob();

        var scheduledOrchestrationId = Events[0].CorrelationId;

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(instantOrchestrationId);

        var instantOrchestrationEvents = Events.FilterByOrchestrationId(instantOrchestrationId);
        instantOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        FakeTimer.Advance(TimeSpan.FromHours(1));

        await WaitForOrchestrationCompletion(scheduledOrchestrationId);

        var scheduledOrchestrationEvents = Events.FilterByOrchestrationId(scheduledOrchestrationId);
        scheduledOrchestrationEvents.ShouldBeScheduledThenCompleted<DummyJob>();

        // Scheduled orchestration should have started before the instant job related one...
        scheduledOrchestrationEvents[0].Timestamp.ShouldBeLessThan(instantOrchestrationEvents[0].Timestamp);

        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TriggeringInstantJobDoesNotDuplicateCronExecutions()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("CRON")));

        await StartNCronJob();

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("INSTANT", CancellationToken);

        await WaitForOrchestrationCompletion(instantOrchestrationId);

        const int cronSlots = 3;
        for (var slot = 1; slot <= cronSlots; slot++)
        {
            FakeTimer.Advance(TimeSpan.FromMinutes(1));
            await WaitForNthOrchestrationState(ExecutionState.OrchestrationCompleted, 1 + slot);
        }

        await WaitForNthOrchestrationState(ExecutionState.OrchestrationCompleted, 1 + cronSlots);

        Storage.Entries.Count(e => e.EndsWith("INSTANT", StringComparison.Ordinal)).ShouldBe(1);
        Storage.Entries.Count(e => e.EndsWith("CRON", StringComparison.Ordinal)).ShouldBe(cronSlots);
    }

    [Fact]
    public async Task MinimalJobApiCanBeUsedForTriggeringCronJobs()
    {
        ServiceCollection.AddNCronJob((Storage storage) =>
        {
            storage.Add("Done");
        }, Cron.AtEveryMinute);

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
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

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var initializingOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.Initializing,
            2);

        initializingOrchestrationEvents.Count.ShouldBe(2);
        initializingOrchestrationEvents[0].CorrelationId.ShouldNotBe(initializingOrchestrationEvents[1].CorrelationId);
    }

    [Fact]
    public async Task InstantJobHasHigherPriorityThanCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("CRON")));
        ServiceCollection.AddSingleton(new ConcurrencySettings { MaxDegreeOfParallelism = 1 });

        await StartNCronJob();

        var scheduledOrchestrationId = Events[0].CorrelationId;

        var instantOrchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>("INSTANT", CancellationToken);

        await WaitForOrchestrationCompletion(instantOrchestrationId);

        var scheduledOrchestrationEvents = Events.FilterByOrchestrationId(scheduledOrchestrationId);
        scheduledOrchestrationEvents.Select(e => e.State).ShouldBe(
            [ExecutionState.OrchestrationStarted, ExecutionState.NotStarted, ExecutionState.Scheduled]);

        var instantOrchestrationEvents = Events.FilterByOrchestrationId(instantOrchestrationId);
        instantOrchestrationEvents.ShouldBeInstantThenCompleted<DummyJob>();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: INSTANT");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TriggeringInstantJobWithoutRegisteringContinuesToWork()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        Action act = () => ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task ExecuteAnInstantJobDelegate()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob((Storage storage) =>
        {
            storage.Add("Done");
        }, CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
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

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeAndWaitForStateCount(
            TimeSpan.FromMinutes(1),
            advances: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(Events, e));

        Storage.Entries.ShouldAllBe(e => e == "true");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task StaticAnonymousJobsCanBeExecutedMultipleTimes()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(JobMethods.WriteTrueStaticAsync, Cron.AtEveryMinute));

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeAndWaitForStateCount(
            TimeSpan.FromMinutes(1),
            advances: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(Events, e));

        Storage.Entries.ShouldAllBe(e => e == "true");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public void CanAddUntypedJobsWithTheSameDelegateWithDifferentNames()
    {
        Action act = () => ServiceCollection.AddNCronJob(
            n => n.AddJob(untypedJob, Cron.AtEveryMinute)
                .AddJob(untypedJob, Cron.AtEveryMinute, jobName: "one")
                .AddJob(untypedJob, Cron.AtEveryMinute, jobName: "another"));

        act.ShouldNotThrow();
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

        await StartNCronJob();

        var countJobs = ServiceProvider.GetRequiredService<JobRegistry>().GetAllCronJobs().Count;
        countJobs.ShouldBe(2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: 1");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: 2");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    [SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    public async Task AddJobWithTypeAsParameterAddsJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(typeof(DummyJob), p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(typeof(IList<>))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(GuidGenerator))]
    [SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    public void AddJobWithTypeDoesNotSupportAnyRandomTypes(Type type)
    {
        Action act = () => ServiceCollection.AddNCronJob(n => n.AddJob(type, p => p.WithCronExpression(Cron.AtEveryMinute)));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task AddingRuntimeJobsWillNotCauseDuplicatedExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>().AddJob<AnotherDummyJob>());

        await StartNCronJob();

        Events.Count.ShouldBe(0);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        registry.TryRegister(n => n.AddJob<AnotherDummyJob>(p => p.WithCronExpression("0 0 10 * *")));

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            1);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CallingAddNCronJobMultipleTimesWillRegisterAllJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        ServiceCollection.AddNCronJob(n => n.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        Storage.Entries.ShouldContain("DummyJob - Parameter: ");
        Storage.Entries.ShouldContain("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PassedInExpressionShouldBePassedOn()
    {
        const string expression = "     0 0 1 * *";
        ServiceCollection.AddNCronJob(n => n
            .AddJob(() => { }, expression, jobName: "job1")
            .AddJob<DummyJob>(p => p.WithName("job2").WithCronExpression(expression)));
        await StartNCronJob();

        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobs = runtimeJobRegistry.GetAllRecurringJobs();

        jobs.First().CronExpression.ShouldBe(expression);
        jobs.Last().CronExpression.ShouldBe(expression);

        runtimeJobRegistry.TryGetSchedule("job1", out var cron1, out _).ShouldBeTrue();
        cron1.ShouldBe(expression);

        runtimeJobRegistry.TryGetSchedule("job2", out var cron2, out _).ShouldBeTrue();
        cron2.ShouldBe(expression);
    }

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public void CanDetectInvalidRegistrations(Action<NCronJobOptionBuilder> register)
    {
        var act = () => ServiceCollection.AddNCronJob(register);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(ValidRegistrations))]
    public void SupportsValidRegistrations(Action<NCronJobOptionBuilder> register)
    {
        var act = () => ServiceCollection.AddNCronJob(register);

        act.ShouldNotThrow();
    }

    public static TheoryData<Action<NCronJobOptionBuilder>> InvalidRegistrations = new()
    {
        {
            // Names should be unique
            s => s.AddJob<DummyJob>(p => p
                    .WithName("JobName")
                    .And
                    .WithName("JobName"))
        },
        {
            // Names should be unique
            n => n.AddJob(() => { }, Cron.AtEveryMinute, jobName: "JobName")
                  .AddJob(() => { }, Cron.AtMinute0, jobName: "JobName")
        },
        {
            // Pure duplicate registration
            s =>
            {
                s.AddJob<DummyJob>();
                s.AddJob<DummyJob>();
            }
        },
        {
            // Pure duplicate registration
            s =>
            {
                s.AddJob(JobDelegate, Cron.AtEveryMinute);
                s.AddJob(JobDelegate, Cron.AtEveryMinute);
            }
        },
        {
            // Pure duplicate registration
            s => s.AddJob<DummyJob>(p => p
                    .WithCronExpression(Cron.AtEveryMinute)
                    .And
                    .WithCronExpression(Cron.AtEveryMinute))
        },
        {
            // No way to invoke DummyJob inambiguously
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one")
                    .And
                    .WithParameter("two"))
        },
        {
            // Pure duplicate registration
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one")
                    .And
                    .WithParameter("one")).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p
                    .WithName("JobName").RunAtStartup()).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p
                    .WithName("JobName")
                    .WithCronExpression(Cron.AtEveryMinute)
                    .RunAtStartup()).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one").RunAtStartup()).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p.RunAtStartup()).RunAtStartup()
        },
    };

    public static TheoryData<Action<NCronJobOptionBuilder>> ValidRegistrations = new()
    {
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithCronExpression(Cron.AtEveryJanuaryTheFirst, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"))
                    .And
                    .WithCronExpression(Cron.AtEveryJanuaryTheFirst, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")))
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithCronExpression(Cron.AtEveryMinute).WithParameter("one")
                    .And
                    .WithCronExpression(Cron.AtEveryMinute).WithParameter("two"))
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one"))
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one")
                    .And
                    .WithParameter("two")).RunAtStartup()
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one").RunAtStartup()
                    .And
                    .WithParameter("two").RunAtStartup())
        },
        {
            s => {
                s.AddJob<DummyJob>(p => p.WithName("Job1").WithCronExpression(Cron.AtEveryMinute))
                    .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
                s.AddJob<DummyJob>(p => p.WithName("Job2").WithCronExpression(Cron.AtEveryMinute))
                    .ExecuteWhen(success: s => s.RunJob<LongRunningJob>());
            }
        },
    };

    private async Task StartNCronJobAndExecuteInstantTypedJob()
    {
        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenCompleted<DummyJob>();
    }

    private async Task StartNCronJobAndExecuteInstantUntypedJob(Func<IInstantJobRegistry, CancellationToken, Guid> jobRunner)
    {
        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = jobRunner(instantJobRegistry, CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenCompleted();
    }

    private static bool OrchestrationIsScheduledThenCompleted<T>(IList<ExecutionProgress> events, ExecutionProgress executionProgress)
    {
        var filteredEvents = events.FilterByOrchestrationId(executionProgress.CorrelationId);
        filteredEvents.ShouldBeScheduledThenCompleted<T>();
        return true;
    }

    private static bool OrchestrationIsScheduledThenCompleted(IList<ExecutionProgress> events, ExecutionProgress executionProgress)
    {
        var filteredEvents = events.FilterByOrchestrationId(executionProgress.CorrelationId);
        filteredEvents.ShouldBeScheduledThenCompleted();
        return true;
    }

    private readonly Delegate untypedJob = (IJobExecutionContext context, Storage storage, CancellationToken token)
        => { storage.Add($"Done - Parameter : {context.Parameter}"); };

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
