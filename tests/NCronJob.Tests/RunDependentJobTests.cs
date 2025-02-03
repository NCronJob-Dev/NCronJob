using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class RunDependentJobTests : JobIntegrationBase
{
    [Fact]
    public async Task WhenJobWasSuccessful_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("Message")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DependentJob: Message Parent: Success");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenJobWasFailed_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("Message")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(false, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("PrincipalJob: Failed");
        Storage.Entries[1].ShouldBe("DependentJob: Message Parent: Failed");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemovingAJobShouldAlsoRemoveItsDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<MainJob>()
            .ExecuteWhen(success: s => s.RunJob<SubMainJob>()));

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        Guid orchestrationId = instantJobRegistry.ForceRunInstantJob<MainJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        Storage.Entries[0].ShouldBe(nameof(MainJob));
        Storage.Entries[1].ShouldBe(nameof(SubMainJob));
        Storage.Entries.Count.ShouldBe(2);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<MainJob>();
        registry.TryRegister(n => n.AddJob<MainJob>());

        Storage.Entries.Clear();

        Guid secondRunOrchestrationId = instantJobRegistry.ForceRunInstantJob<MainJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(events, secondRunOrchestrationId);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe(nameof(MainJob));
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CorrelationIdIsSharedByJobsAndTheirDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalCorrelationIdJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries.Distinct().Count().ShouldBe(1);
        Storage.Entries.First().ShouldBe(orchestrationId.ToString());
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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalCorrelationIdJob>(parameter: true, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries.Count.ShouldBe(1);

        Assert.All(events, e => Assert.Equal(orchestrationId, e.CorrelationId));
        Assert.Equal(ExecutionState.OrchestrationStarted, events[0].State);
        Assert.Equal(ExecutionState.NotStarted, events[1].State);
        Assert.Equal(ExecutionState.Initializing, events[2].State);
        Assert.Equal(ExecutionState.Running, events[3].State);
        Assert.Equal(ExecutionState.Completing, events[4].State);
        Assert.Equal(ExecutionState.WaitingForDependency, events[5].State);

        // Assess the dependent jobs
        Assert.Equal(events[1].RunId, events[6].ParentRunId);
        Assert.Equal(ExecutionState.NotStarted, events[6].State);
        Assert.Equal(events[1].RunId, events[7].ParentRunId);
        Assert.Equal(ExecutionState.Skipped, events[7].State);
        Assert.Equal(events[1].RunId, events[8].ParentRunId);
        Assert.Equal(ExecutionState.NotStarted, events[8].State);
        Assert.Equal(events[1].RunId, events[9].ParentRunId);
        Assert.Equal(ExecutionState.Skipped, events[9].State);

        Assert.NotEqual(events[6].RunId, events[8].RunId);

        Assert.Equal(ExecutionState.Completed, events[10].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, events[11].State);
        Assert.Equal(12, events.Count);
    }

    [Fact]
    public async Task WhenJobWasSuccessful_DependentAnonymousJobShouldRun()
    {
        Func<Storage, JobExecutionContext, Task> execution = (storage, context) =>
        {
            storage.Entries.Add($"Parent: {context.ParentOutput}");
            return Task.CompletedTask;
        };

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob(execution)));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DependentJob:  Parent: Success");
        Storage.Entries[2].ShouldBe("Dependent job did run");
        Storage.Entries.Count.ShouldBe(3);

        Assert.All(events, e => Assert.Equal(orchestrationId, e.CorrelationId));
        Assert.Equal(ExecutionState.OrchestrationStarted, events[0].State);
        Assert.Equal(ExecutionState.Completed, events[17].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, events[18].State);
        Assert.Equal(19, events.Count);
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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        await WaitForOrchestrationCompletion(events, events.First().CorrelationId);

        subscription.Dispose();

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
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Entries.Add("1")));
            n.AddJob<PrincipalJob>(s => s.WithCronExpression("1 0 2 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Entries.Add("2")));
        });

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var firstOrchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, firstOrchestrationId);

        Storage.Entries.ShouldContain("PrincipalJob: Success");
        Storage.Entries.ShouldContain("1");

        Storage.Entries.Clear();
        FakeTimer.Advance(TimeSpan.FromDays(1));

        await WaitUntilConditionIsMet(events, events => events.Any(e => e.State == ExecutionState.OrchestrationCompleted
            && e.CorrelationId != firstOrchestrationId));

        subscription.Dispose();

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("2");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenJobIsNotCreated_DependentFailureJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<JobThatThrowsInCtor>()
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("After Exception")));

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<JobThatThrowsInCtor>(false, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

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
            storage.Entries.Add(context.CorrelationId.ToString());
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
