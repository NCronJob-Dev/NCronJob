using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class InstantJobExecutionTests : JobIntegrationBase
{
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

        await StartNCronJobAndExecuteInstantUntypedJob((ijr, token) => IInstantJobRegistryExtensions.ForceRunInstantJob(ijr, UntypedJob, token));

        await StartNCronJobAndExecuteInstantUntypedJob((ijr, token) => ijr.ForceRunScheduledJob(UntypedJob, TimeSpan.Zero, token));
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
}
