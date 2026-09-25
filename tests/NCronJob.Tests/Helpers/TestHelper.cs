using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCronJob.Tests;

public abstract class JobIntegrationBase : IDisposable
{
    private ServiceProvider? serviceProvider;
    private ExecutionProgressMonitor? progressMonitor;

    protected CancellationToken CancellationToken { get; }
    protected ServiceCollection ServiceCollection { get; }
    protected TimerAwareFakeTimeProvider FakeTimer { get; }
    protected Storage Storage { get; }
    protected IList<ExecutionProgress> Events => progressMonitor?.Events ?? [];

    protected static Delegate UntypedJob { get; } = (IJobExecutionContext context, Storage storage, CancellationToken token)
        => { storage.Add($"Done - Parameter : {context.Parameter}"); };

    public static TheoryData<Func<IInstantJobRegistry, TimeProvider, object?, CancellationToken, Guid>> InstantJobRunners()
    {
        var t = new TheoryData<Func<IInstantJobRegistry, TimeProvider, object?, CancellationToken, Guid>>();
        t.Add((i, f, p, t) => i.RunInstantJob<DummyJob>(p, t));
        t.Add((i, f, p, t) => i.RunScheduledJob<DummyJob>(f.GetUtcNow(), p, t));
        t.Add((i, f, p, t) => i.ForceRunInstantJob<DummyJob>(p, t));
        t.Add((i, f, p, t) => i.ForceRunScheduledJob<DummyJob>(TimeSpan.Zero, p, t));
        return t;
    }

    protected JobIntegrationBase()
    {
        FakeTimer = new() { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };

        var cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken = cancellationToken;

        ServiceCollection = new();
        ServiceCollection.AddLogging();
        ServiceCollection.AddSingleton<IHostApplicationLifetime, MockHostApplicationLifetime>();
        ServiceCollection.AddSingleton<TimeProvider>(FakeTimer);

        Storage = new(FakeTimer);
        ServiceCollection.AddSingleton(Storage);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

#pragma warning disable IDISP023 // Don't use reference types in finalizer context
        // False positive (cf. https://github.com/DotNetAnalyzers/IDisposableAnalyzers/issues/176)

        progressMonitor?.Dispose();

        serviceProvider?.Dispose();

        TestFailureHelper.DumpContext(Storage, Events);
#pragma warning restore IDISP023 // Don't use reference types in finalizer context
    }

    protected ServiceProvider ServiceProvider => serviceProvider ??= ServiceCollection.BuildServiceProvider();

    protected Task<IList<ExecutionProgress>> WaitForNthOrchestrationState(ExecutionState state, int howMany) =>
        ProgressMonitor.WaitForCountAsync(state, howMany);

    protected Task WaitForOrchestrationCompletion(Guid orchestrationId) =>
        ProgressMonitor.WaitForStateAsync(orchestrationId, ExecutionState.OrchestrationCompleted);

    protected Task WaitForOrchestrationState(Guid orchestrationId, ExecutionState state) =>
        ProgressMonitor.WaitForStateAsync(orchestrationId, state);

    protected Task AdvanceTimeUntilOrchestrationCompletion(Guid orchestrationId) =>
        AdvanceTimeUntilAsync(ProgressMonitor.WaitForStateAsync(orchestrationId, ExecutionState.OrchestrationCompleted));

    protected Task AdvanceTimeUntilOrchestrationState(Guid orchestrationId, ExecutionState state) =>
        AdvanceTimeUntilAsync(ProgressMonitor.WaitForStateAsync(orchestrationId, state));

    protected Task<IList<ExecutionProgress>> AdvanceTimeUntilStateCount(ExecutionState state, int count) =>
        AdvanceTimeUntilAsync(ProgressMonitor.WaitForCountAsync(state, count));

    protected Task<ExecutionProgress> WaitForJobState(
        ExecutionState state,
        string? name = null,
        Type? type = null) =>
        ProgressMonitor.WaitForStateAsync(state, name, type);

    protected async Task<IList<ExecutionProgress>> AdvanceTimeStepwiseUntilStateCount(
        TimeSpan interval,
        int steps,
        ExecutionState state,
        int expectedCount)
    {
        for (var step = 1; step <= steps; step++)
        {
            FakeTimer.Advance(interval);
            await WaitForNthOrchestrationState(state, Math.Min(step, expectedCount));
        }

        return await WaitForNthOrchestrationState(state, expectedCount);
    }

    private async Task<T> AdvanceTimeUntilAsync<T>(Task<T> task)
    {
        const int maximumAdvances = 500;
        const int quietWindowsBeforeFastForward = 3;
        var remainingQuietWindows = quietWindowsBeforeFastForward;

        for (var advance = 0; advance < maximumAdvances && !task.IsCompleted; advance++)
        {
            var progressChanged = ProgressMonitor.WaitForChangeAsync();
            var firedTimerCountBeforeAdvance = FakeTimer.FiredTimerCount;

            FakeTimer.Advance(TimeSpan.FromSeconds(1));

            if (FakeTimer.FiredTimerCount != firedTimerCountBeforeAdvance || ProgressMonitor.HasActiveRuns)
            {
                remainingQuietWindows = quietWindowsBeforeFastForward;
            }

            if (remainingQuietWindows == 0 && !progressChanged.IsCompleted)
            {
                continue;
            }

            await Task.WhenAny(
                task,
                progressChanged,
                Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken));

            remainingQuietWindows = progressChanged.IsCompleted
                ? quietWindowsBeforeFastForward
                : remainingQuietWindows - 1;
        }

        return await task;
    }

    protected async Task StartNCronJob()
    {
        _ = ProgressMonitor;
        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
    }

    protected ExecutionProgressMonitor CreateExecutionProgressMonitor(IServiceProvider serviceProvider) =>
        new(serviceProvider, CancellationToken);

    private ExecutionProgressMonitor ProgressMonitor =>
        progressMonitor ??= CreateExecutionProgressMonitor(ServiceProvider);
}
