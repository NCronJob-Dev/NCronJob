using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RunDependentJobTests : JobIntegrationBase
{
    [Fact]
    public async Task WhenJobWasSuccessful_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("Message")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DependentJob: Message Parent: Success");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenJobWasFailed_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("Message")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(false, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Failed");
        Storage.Entries[1].ShouldBe("DependentJob: Message Parent: Failed");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemovingAJobShouldAlsoRemoveItsDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<MainJob>()
            .ExecuteWhen(success: s => s.RunJob<SubMainJob>()));

        await StartNCronJob(startMonitoringEvents: true);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRegistry.ForceRunInstantJob<MainJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe(nameof(MainJob));
        Storage.Entries[1].ShouldBe(nameof(SubMainJob));
        Storage.Entries.Count.ShouldBe(2);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<MainJob>();
        registry.TryRegister(n => n.AddJob<MainJob>());

        var secondRunOrchestrationId = instantJobRegistry.ForceRunInstantJob<MainJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(secondRunOrchestrationId, stopMonitoringEvents: true);

        Storage.Entries[2].ShouldBe(nameof(MainJob));
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CorrelationIdIsSharedByJobsAndTheirDependencies()
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
    public async Task SkipChildrenShouldPreventDependentJobsFromRunning()
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
    public async Task WhenJobWasSuccessful_DependentAnonymousJobShouldRun()
    {
        Func<Storage, JobExecutionContext, Task> execution = (storage, context) =>
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
    public async Task CanBuildAChainOfDependentJobsThatRunAfterOneJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("1").RunJob<DependentJob>("2"))
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("3")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DependentJob: 1 Parent: Success");
        Storage.Entries[2].ShouldBe("DependentJob: 2 Parent: Success");
        Storage.Entries[3].ShouldBe("DependentJob: 3 Parent: Success");
        Storage.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task CanTriggerAChainOfDependentJobs()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DependentJob>());
            n.AddJob<DependentJob>().ExecuteWhen(success: s => s.RunJob<DependentDependentJob>());
        });

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DependentJob:  Parent: Success");
        Storage.Entries[2].ShouldBe("Dependent job did run");
        Storage.Entries.Count.ShouldBe(3);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[17].State.ShouldBe(ExecutionState.Completed);
        Events[18].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(19);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task CanBuildAScheduledChainOfDependentJobs()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>(o => o.WithCronExpression(Cron.AtEveryMinute).WithParameter(true))
                .ExecuteWhen(success: s => s.RunJob<DependentJob>());
            n.AddJob<DependentJob>(o => o.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<DependentDependentJob>());
        });

        await StartNCronJob(startMonitoringEvents: true);

        await WaitForOrchestrationCompletion(Events[0].CorrelationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DependentJob:  Parent: Success");
        Storage.Entries[2].ShouldBe("Dependent job did run");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ConfiguringDifferentDependentJobsForSchedulesShouldResultInIndependentRuns()
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
    public async Task WhenJobIsNotCreated_DependentFailureJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<JobThatThrowsInCtor>()
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("After Exception")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<JobThatThrowsInCtor>(false, token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("DependentJob: After Exception Parent: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    private sealed class PrincipalJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
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

            storage.Add($"{nameof(PrincipalJob)}: {context.Output}");

            maybeThrow();

            return Task.CompletedTask;
        }
    }

    private sealed class DependentJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add($"{nameof(DependentJob)}: {context.Parameter} Parent: {context.ParentOutput}");
            return Task.CompletedTask;
        }
    }

    private sealed class DependentDependentJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("Dependent job did run");
            return Task.CompletedTask;
        }
    }
    private sealed class PrincipalCorrelationIdJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(context.CorrelationId.ToString());

            if (context.Parameter is true)
            {
                context.SkipChildren();
            }

            return Task.CompletedTask;
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

    private sealed class MainJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(nameof(MainJob));
            return Task.CompletedTask;
        }
    }

    private sealed class SubMainJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(nameof(SubMainJob));
            return Task.CompletedTask;
        }
    }
}
