using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RunDependentJobTests : JobIntegrationBase
{
    [Fact]
    public async Task WhenTypedJobWasSuccessful_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenTypedJobWasFailed_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(faulted: s => s.RunJob<DummyJob>("Message")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(false, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Failed");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemovingATypedJobShouldAlsoRemoveItsDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>()
            .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>()));

        await StartNCronJob(startMonitoringEvents: true);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRegistry.ForceRunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<DummyJob>();
        registry.TryRegister(n => n.AddJob<DummyJob>());

        var secondRunOrchestrationId = instantJobRegistry.ForceRunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(secondRunOrchestrationId, stopMonitoringEvents: true);

        Storage.Entries[2].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CorrelationIdIsSharedByTypedJobsAndTheirDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalCorrelationIdJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries.Distinct().Count().ShouldBe(1);
        Storage.Entries[0].ShouldBe(orchestrationId.ToString());
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SkipChildrenOfTypedJobShouldPreventDependentJobsFromRunning()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalCorrelationIdJob>()
                .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>())
                .ExecuteWhen(success: s => s.RunJob((Storage storage) => storage.Add("1")));
        });

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalCorrelationIdJob>(parameter: true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(1);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[1].State.ShouldBe(ExecutionState.NotStarted);
        Events[2].State.ShouldBe(ExecutionState.Initializing);
        Events[3].State.ShouldBe(ExecutionState.Running);
        Events[4].State.ShouldBe(ExecutionState.Completing);
        Events[5].State.ShouldBe(ExecutionState.WaitingForDependency);

        // Assess the dependent jobs
        Events[1].RunId.ShouldBe(Events[6].ParentRunId);
        Events[6].State.ShouldBe(ExecutionState.NotStarted);
        Events[1].RunId.ShouldBe(Events[7].ParentRunId);
        Events[7].State.ShouldBe(ExecutionState.Skipped);
        Events[1].RunId.ShouldBe(Events[8].ParentRunId);
        Events[8].State.ShouldBe(ExecutionState.NotStarted);
        Events[1].RunId.ShouldBe(Events[9].ParentRunId);
        Events[9].State.ShouldBe(ExecutionState.Skipped);

        Events[6].RunId.ShouldNotBe(Events[8].RunId);

        Events[10].State.ShouldBe(ExecutionState.Completed);
        Events[11].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(12);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task WhenTypedJobWasSuccessful_DependentAnonymousJobShouldRun()
    {
        Func<Storage, IJobExecutionContext, Task> execution = (storage, context) =>
        {
            storage.Add($"Parent: {context.ParentOutput}");
            return Task.CompletedTask;
        };

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob(execution)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("Parent: Success");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CanBuildAChainOfDependentJobsThatRunAfterOneTypedJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("1").RunJob<DummyJob>("2"))
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("3")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: 1");
        Storage.Entries[2].ShouldBe("DummyJob - Parameter: 2");
        Storage.Entries[3].ShouldBe("DummyJob - Parameter: 3");
        Storage.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task CanTriggerAChainOfDependentJobsForATypedJob()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>().ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[17].State.ShouldBe(ExecutionState.Completed);
        Events[18].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(19);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task CanBuildAScheduledChainOfDependentJobsForATypedJob()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>(o => o.WithCronExpression(Cron.AtEveryMinute).WithParameter(true))
                .ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob(startMonitoringEvents: true);

        await WaitForOrchestrationCompletion(Events[0].CorrelationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ConfiguringDifferentDependentTypedJobsForSchedulesShouldResultInIndependentRuns()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>(s => s.WithCronExpression("1 0 1 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("1")));
            n.AddJob<PrincipalJob>(s => s.WithCronExpression("1 0 2 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("2")));
        });

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var firstOrchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(firstOrchestrationId);

        Storage.Entries.ShouldContain("PrincipalJob: Success");
        Storage.Entries.ShouldContain("1");

        FakeTimer.Advance(TimeSpan.FromDays(1));

        await WaitForNthOrchestrationState(ExecutionState.OrchestrationCompleted, 2, stopMonitoringEvents: true);

        Storage.Entries[2].ShouldBe("PrincipalJob: Success");
        Storage.Entries[3].ShouldBe("2");
        Storage.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task WhenTypedJobIsNotCreated_DependentFailureJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<JobThatThrowsInCtor>()
            .ExecuteWhen(faulted: s => s.RunJob<DummyJob>("After Exception")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<JobThatThrowsInCtor>(false, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: After Exception");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DependentJobOfTypedJobShouldHandleParametersCorrectly()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>("overridden"));
            n.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.Never).WithParameter("dependent"));
        });

        await StartNCronJob(startMonitoringEvents: true);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var rootOrchestrationId = instantJobRegistry.ForceRunInstantJob<DummyJob>(null, token: CancellationToken);
        await WaitForOrchestrationCompletion(rootOrchestrationId);

        var dependentOrchestrationId = instantJobRegistry.ForceRunInstantJob<AnotherDummyJob>(null, token: CancellationToken);
        await WaitForOrchestrationCompletion(dependentOrchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: overridden");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: dependent");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task WhenUnTypedJobWasSuccessful_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(PrincipalJobUntyped, p => p.WithName("principal"))
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob("principal", true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob (untyped): Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenUnTypedJobWasFailed_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(PrincipalJobUntyped, p => p.WithName("principal"))
            .ExecuteWhen(faulted: s => s.RunJob<DummyJob>("Message")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob("principal", false, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob (untyped): Failed");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemovingAUnTypedJobShouldAlsoRemoveItsDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(UntypedJob.Dummy, p => p.WithName("dummy"))
            .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>()));

        await StartNCronJob(startMonitoringEvents: true);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRegistry.ForceRunInstantJob("dummy", token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob (untyped) - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob("dummy");
        registry.TryRegister(n => n.AddJob(UntypedJob.Dummy, Cron.Never, jobName: "dummy"));

        var secondRunOrchestrationId = instantJobRegistry.ForceRunInstantJob("dummy", token: CancellationToken);

        await WaitForOrchestrationCompletion(secondRunOrchestrationId, stopMonitoringEvents: true);

        Storage.Entries[2].ShouldBe("DummyJob (untyped) - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CorrelationIdIsSharedByUnTypedJobsAndTheirDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(PrincipalCorrelationIdJobUntyped, p => p.WithName("principalCorrelation"))
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob("principalCorrelation", token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries.Distinct().Count().ShouldBe(1);
        Storage.Entries[0].ShouldBe(orchestrationId.ToString());
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SkipChildrenOfUnTypedJobShouldPreventDependentJobsFromRunning()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(PrincipalCorrelationIdJobUntyped, p => p.WithName("principalCorrelation"))
                .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>())
                .ExecuteWhen(success: s => s.RunJob((Storage storage) => storage.Add("1")));
        });

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob("principalCorrelation", parameter: true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(1);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[1].State.ShouldBe(ExecutionState.NotStarted);
        Events[2].State.ShouldBe(ExecutionState.Initializing);
        Events[3].State.ShouldBe(ExecutionState.Running);
        Events[4].State.ShouldBe(ExecutionState.Completing);
        Events[5].State.ShouldBe(ExecutionState.WaitingForDependency);

        // Assess the dependent jobs
        Events[1].RunId.ShouldBe(Events[6].ParentRunId);
        Events[6].State.ShouldBe(ExecutionState.NotStarted);
        Events[1].RunId.ShouldBe(Events[7].ParentRunId);
        Events[7].State.ShouldBe(ExecutionState.Skipped);
        Events[1].RunId.ShouldBe(Events[8].ParentRunId);
        Events[8].State.ShouldBe(ExecutionState.NotStarted);
        Events[1].RunId.ShouldBe(Events[9].ParentRunId);
        Events[9].State.ShouldBe(ExecutionState.Skipped);

        Events[6].RunId.ShouldNotBe(Events[8].RunId);

        Events[10].State.ShouldBe(ExecutionState.Completed);
        Events[11].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(12);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task WhenUnTypedJobWasSuccessful_DependentAnonymousJobShouldRun()
    {
        Func<Storage, IJobExecutionContext, Task> execution = (storage, context) =>
        {
            storage.Add($"Parent: {context.ParentOutput}");
            return Task.CompletedTask;
        };

        ServiceCollection.AddNCronJob(n => n.AddJob(PrincipalJobUntyped, p => p.WithName("principal"))
            .ExecuteWhen(success: s => s.RunJob(execution)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob("principal", true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob (untyped): Success");
        Storage.Entries[1].ShouldBe("Parent: Success");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CanBuildAChainOfDependentJobsThatRunAfterOneUnTypedJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(PrincipalJobUntyped, p => p.WithName("principal"))
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("1").RunJob<DummyJob>("2"))
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("3")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob("principal", true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob (untyped): Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: 1");
        Storage.Entries[2].ShouldBe("DummyJob - Parameter: 2");
        Storage.Entries[3].ShouldBe("DummyJob - Parameter: 3");
        Storage.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task CanTriggerAChainOfDependentJobsForAUnTypedJob()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(PrincipalJobUntyped, p => p.WithName("principal")).ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>().ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob("principal", true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob (untyped): Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[17].State.ShouldBe(ExecutionState.Completed);
        Events[18].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(19);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task CanBuildAScheduledChainOfDependentJobsForAUnTypedJob()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(PrincipalJobUntyped, p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter(true))
                .ExecuteWhen(success: s => s.RunJob<DummyJob>())
            .AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob(startMonitoringEvents: true);

        await WaitForOrchestrationCompletion(Events[0].CorrelationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob (untyped): Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ConfiguringDifferentDependentUnTypedJobsForSchedulesShouldResultInIndependentRuns()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(PrincipalJobUntyped, p => p.WithCronExpression("1 0 1 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("1")));
            n.AddJob(PrincipalJobUntyped, p => p.WithCronExpression("1 0 2 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("2")));
        });

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var firstOrchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(firstOrchestrationId);

        Storage.Entries.ShouldContain("PrincipalJob (untyped): Success");
        Storage.Entries.ShouldContain("1");

        FakeTimer.Advance(TimeSpan.FromDays(1));

        await WaitForNthOrchestrationState(ExecutionState.OrchestrationCompleted, 2, stopMonitoringEvents: true);

        Storage.Entries[2].ShouldBe("PrincipalJob (untyped): Success");
        Storage.Entries[3].ShouldBe("2");
        Storage.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task DependentJobOfUnTypedJobShouldHandleParametersCorrectly()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(UntypedJob.Dummy, p => p.WithName("dummy").WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>("overridden"));
            n.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.Never).WithParameter("dependent"));
        });

        await StartNCronJob(startMonitoringEvents: true);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var rootOrchestrationId = instantJobRegistry.ForceRunInstantJob("dummy", token: CancellationToken);
        await WaitForOrchestrationCompletion(rootOrchestrationId);

        var dependentOrchestrationId = instantJobRegistry.ForceRunInstantJob<AnotherDummyJob>(null, token: CancellationToken);
        await WaitForOrchestrationCompletion(dependentOrchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("DummyJob (untyped) - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: overridden");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: dependent");
        Storage.Entries.Count.ShouldBe(3);
    }

    private sealed class PrincipalJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            PrincipalJobImplementation(GetType().Name, storage, context, token);

            return Task.CompletedTask;
        }
    }

    private static Delegate PrincipalJobUntyped
        => (Storage storage, IJobExecutionContext context, CancellationToken token)
            => PrincipalJobImplementation($"{nameof(PrincipalJob)} (untyped)", storage, context, token);

    private static void PrincipalJobImplementation(
        string name,
        Storage storage,
        IJobExecutionContext context,
#pragma warning disable S1172 // Unused method parameters should be removed
        CancellationToken token)
#pragma warning restore S1172 // Unused method parameters should be removed
    {
        Action maybeThrow = () => { };

        if (context.Parameter is true)
        {
            context.Output = "Success";
        }
        else
        {
            context.Output = "Failed";
            maybeThrow = () => throw new InvalidOperationException("Failed");
        }

        storage.Add($"{name}: {context.Output}");

        maybeThrow();
    }

    private sealed class PrincipalCorrelationIdJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            PrincipalCorrelationIdJobImplementation(storage, context, token);

            return Task.CompletedTask;
        }
    }

    private static Delegate PrincipalCorrelationIdJobUntyped
        => (Storage storage, IJobExecutionContext context, CancellationToken token)
            => PrincipalCorrelationIdJobImplementation(storage, context, token);

    private static void PrincipalCorrelationIdJobImplementation(
        Storage storage,
        IJobExecutionContext context,
#pragma warning disable S1172 // Unused method parameters should be removed
        CancellationToken token)
#pragma warning restore S1172 // Unused method parameters should be removed
    {
        storage.Add(context.CorrelationId.ToString());

        if (context.Parameter is true)
        {
            context.SkipChildren();
        }
    }

    private sealed class DependentCorrelationIdJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(context.CorrelationId.ToString());
            return Task.CompletedTask;
        }
    }
}
