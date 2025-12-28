using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class ConditionalJobTests : JobIntegrationBase
{
    [Fact]
    public async Task JobWithSimplePredicateConditionShouldExecuteWhenTrue()
    {
        var shouldRun = true;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => shouldRun)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        Storage.Entries.Count.ShouldBe(1);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task JobWithSimplePredicateConditionShouldSkipWhenFalse()
    {
        var shouldRun = false;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => shouldRun)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationState(orchestrationId, ExecutionState.Skipped, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(0); // Job never executed
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task JobWithDependencyInjectionConditionShouldWork()
    {
        ServiceCollection.AddSingleton<FeatureFlagService>();
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf((FeatureFlagService flags) => flags.IsEnabled("my-feature"))));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task JobWithAsyncConditionShouldWork()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(async () =>
                {
                    await Task.Delay(10);
                    return true;
                })));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task JobWithAsyncDIConditionShouldWork()
    {
        ServiceCollection.AddSingleton<AsyncFeatureFlagService>();
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(async (AsyncFeatureFlagService flags) => await flags.IsEnabledAsync("my-feature"))));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task MultipleOnlyIfConditionsShouldBeCombinedWithAndLogic()
    {
        var condition1 = true;
        var condition2 = true;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => condition1)
                .OnlyIf(() => condition2)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task MultipleOnlyIfConditionsShouldSkipWhenOneReturnsFalse()
    {
        var condition1 = true;
        var condition2 = false;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => condition1)
                .OnlyIf(() => condition2)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationState(orchestrationId, ExecutionState.Skipped, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(0); // Job never executed
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task ConditionShouldNotBeReEvaluatedDuringRetry()
    {
        var evaluationCount = 0;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<JobThatFailsFirstTime>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() =>
                {
                    evaluationCount++;
                    return true;
                })));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        // Condition should be evaluated only once, even though retry happened
        evaluationCount.ShouldBe(1);
        
        Storage.Entries.Count.ShouldBeGreaterThan(0); // Job executed and retried
    }

    [Fact]
    public async Task ConditionHandlerShouldBeCalledWhenConditionFails()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => false))
            .AddConditionHandler<SimpleJobConditionHandler>());

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationState(orchestrationId, ExecutionState.Skipped, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(1);
        Storage.Entries[0].ShouldBe("SimpleJob condition not met");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task JobShouldNotBeInstantiatedWhenConditionFails()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<JobWithExpensiveConstructor>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => false)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationState(orchestrationId, ExecutionState.Skipped, stopMonitoringEvents: true);

        // Constructor should not be called
        Storage.Entries.Count.ShouldBe(0);
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task MultipleJobExecutionsShouldEvaluateConditionEachTime()
    {
        var runCount = 0;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<CountingJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => runCount++ < 2)));

        await StartNCronJob(startMonitoringEvents: true);

        await WaitForNthOrchestrationState(ExecutionState.Completed, 1);
        
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForNthOrchestrationState(ExecutionState.Completed, 2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForNthOrchestrationState(ExecutionState.Skipped, 1, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ConditionCanAccessCancellationToken()
    {
        var cancellationTokenPassed = false;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf((CancellationToken ct) =>
                {
                    cancellationTokenPassed = !ct.IsCancellationRequested;
                    return true;
                })));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        cancellationTokenPassed.ShouldBeTrue();
        Storage.Entries[0].ShouldBe("SimpleJob executed");
    }

    [Fact]
    public async Task DirectOnlyIfWithoutChainingWhenTrueShouldExecute()
    {
        var shouldRun = true;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .OnlyIf(() => shouldRun)
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        Storage.Entries.Count.ShouldBe(1);
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task DirectOnlyIfWithoutChainingWhenFalseShouldSkip()
    {
        var shouldRun = false;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .OnlyIf(() => shouldRun)
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationState(orchestrationId, ExecutionState.Skipped, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(0); // Job never executed
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task DirectOnlyIfWithDependencyInjectionShouldWork()
    {
        ServiceCollection.AddSingleton<FeatureFlagService>();
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .OnlyIf((FeatureFlagService flags) => flags.IsEnabled("my-feature"))
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task DirectOnlyIfWithAsyncConditionShouldWork()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .OnlyIf(async () =>
                {
                    await Task.Delay(10);
                    return true;
                })
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task DirectOnlyIfCanBeChainedWithOtherOnlyIfs()
    {
        var condition1 = true;
        var condition2 = true;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .OnlyIf(() => condition1)
                .OnlyIf(() => condition2)
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task DirectOnlyIfWithInstantJobShouldWork()
    {
        var shouldRun = true;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p.OnlyIf(() => shouldRun)));

        await StartNCronJob(startMonitoringEvents: true);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();
        var orchestrationId = instantJobRegistry.RunInstantJob<SimpleJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Running);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task DirectOnlyIfWithInstantJobWhenFalseShouldSkip()
    {
        var shouldRun = false;
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p.OnlyIf(() => shouldRun)));

        await StartNCronJob(startMonitoringEvents: true);

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();
        var orchestrationId = instantJobRegistry.RunInstantJob<SimpleJob>(token: CancellationToken);

        await WaitForOrchestrationState(orchestrationId, ExecutionState.Skipped, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(0); // Job never executed
        
        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task ConditionWithMultipleDependenciesShouldResolveAll()
    {
        ServiceCollection.AddSingleton<FeatureFlagService>();
        ServiceCollection.AddSingleton<ConfigService>();
        
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf((FeatureFlagService flags, ConfigService config) => 
                    flags.IsEnabled("my-feature") && config.GetValue("enabled") == "true")));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries[0].ShouldBe("SimpleJob executed");
    }

    // Test helper classes
    private sealed class SimpleJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("SimpleJob executed");
            return Task.CompletedTask;
        }
    }

    private sealed class CountingJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("CountingJob executed");
            return Task.CompletedTask;
        }
    }

    [RetryPolicy(retryCount: 2)]
    private sealed class JobThatFailsFirstTime(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add($"Attempt {context.Attempts}");
            if (context.Attempts == 0)
            {
                throw new InvalidOperationException("First attempt fails");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class JobWithExpensiveConstructor : IJob
    {
        private readonly Storage storage;

        public JobWithExpensiveConstructor(Storage storage)
        {
            this.storage = storage;
            storage.Add("JobWithExpensiveConstructor instantiated");
        }

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("JobWithExpensiveConstructor executed");
            return Task.CompletedTask;
        }
    }

    private sealed class SimpleJobConditionHandler(Storage storage) : IJobConditionHandler<SimpleJob>
    {
        public Task HandleConditionNotMetAsync(JobConditionContext context, CancellationToken cancellationToken)
        {
            storage.Add("SimpleJob condition not met");
            return Task.CompletedTask;
        }
    }

    private sealed class FeatureFlagService
    {
        public bool IsEnabled(string feature) => feature == "my-feature";
    }

    private sealed class AsyncFeatureFlagService
    {
        public async Task<bool> IsEnabledAsync(string feature)
        {
            await Task.Delay(10);
            return feature == "my-feature";
        }
    }

    private sealed class ConfigService
    {
        public string GetValue(string key) => key == "enabled" ? "true" : "false";
    }
}
