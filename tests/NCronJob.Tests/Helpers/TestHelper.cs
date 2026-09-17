using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace NCronJob.Tests;

public abstract class JobIntegrationBase : IDisposable
{
    private ServiceProvider? serviceProvider;
    private ExecutionProgressMonitor? progressMonitor;

    protected CancellationToken CancellationToken { get; }
    protected ServiceCollection ServiceCollection { get; }
    protected FakeTimeProvider FakeTimer { get; }
    protected Storage Storage { get; }
    protected IList<ExecutionProgress> Events => progressMonitor?.Events ?? [];

    protected JobIntegrationBase()
    {
        FakeTimeProvider fakeTimeProvider = new() { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };
        FakeTimer = fakeTimeProvider;

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

    protected async Task<IList<ExecutionProgress>> AdvanceTimeAndWaitForStateCount(
        TimeSpan interval,
        int advances,
        ExecutionState state,
        int expectedCount)
    {
        for (var advance = 1; advance <= advances; advance++)
        {
            FakeTimer.Advance(interval);
            await WaitForNthOrchestrationState(state, Math.Min(advance, expectedCount));
        }

        return await WaitForNthOrchestrationState(state, expectedCount);
    }

    private async Task<T> AdvanceTimeUntilAsync<T>(Task<T> task)
    {
        const int maximumAdvances = 500;

        for (var advance = 0; advance < maximumAdvances && !task.IsCompleted; advance++)
        {
            var progressChanged = ProgressMonitor.WaitForChangeAsync();

            FakeTimer.Advance(TimeSpan.FromSeconds(1));

            await Task.WhenAny(
                task,
                progressChanged,
                Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken));
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
