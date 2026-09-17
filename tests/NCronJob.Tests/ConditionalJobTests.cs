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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationState(orchestrationId, ExecutionState.Skipped);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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
                    await Task.Yield();
                    return true;
                })));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationState(orchestrationId, ExecutionState.Skipped);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        // Condition should be evaluated only once, even though retry happened
        evaluationCount.ShouldBe(1);

        Storage.Entries.Count.ShouldBeGreaterThan(0); // Job executed and retried
    }

    [Fact]
    public async Task ConditionHandlerShouldBeCalledWhenConditionFails()
    {
        var signal = new ConditionHandlerSignal();
        ServiceCollection.AddSingleton(signal);
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p
                .WithCronExpression(Cron.AtEveryMinute)
                .OnlyIf(() => false))
            .AddConditionHandler<SimpleJobConditionHandler>());

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationState(orchestrationId, ExecutionState.Skipped);
        await signal.Completed.Task.WaitAsync(CancellationToken);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationState(orchestrationId, ExecutionState.Skipped);

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

        await StartNCronJob();

        await AdvanceTimeUntilStateCount(ExecutionState.Completed, 1);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await AdvanceTimeUntilStateCount(ExecutionState.Completed, 2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await AdvanceTimeUntilStateCount(ExecutionState.Skipped, 1);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationState(orchestrationId, ExecutionState.Skipped);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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
                    await Task.Yield();
                    return true;
                })
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();
        var orchestrationId = instantJobRegistry.RunInstantJob<SimpleJob>(token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();
        var orchestrationId = instantJobRegistry.RunInstantJob<SimpleJob>(token: CancellationToken);

        await AdvanceTimeUntilOrchestrationState(orchestrationId, ExecutionState.Skipped);

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

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;
        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

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
            return context.Attempts == 0 ? throw new InvalidOperationException("First attempt fails") : Task.CompletedTask;
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

    private sealed class SimpleJobConditionHandler(
        Storage storage,
        ConditionHandlerSignal signal) : IJobConditionHandler<SimpleJob>
    {
        public Task HandleConditionNotMetAsync(JobConditionContext context, CancellationToken cancellationToken)
        {
            storage.Add("SimpleJob condition not met");
            signal.Completed.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class ConditionHandlerSignal
    {
        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FeatureFlagService
    {
        public bool IsEnabled(string feature) => feature == "my-feature";
    }

    private sealed class AsyncFeatureFlagService
    {
        public async Task<bool> IsEnabledAsync(string feature)
        {
            await Task.Yield();
            return feature == "my-feature";
        }
    }

    private sealed class ConfigService
    {
        public string GetValue(string key) => key == "enabled" ? "true" : "false";
    }
}
