using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class ExecutionProgressMonitorTests
{
    [Fact]
    public async Task WaitCanReplayAnEventPublishedBeforeRegistration()
    {
        using var fixture = new MonitorFixture();
        var progress = CreateProgress(ExecutionState.Completed);
        fixture.Reporter.Publish(progress);

        var result = await fixture.Monitor.WaitForStateAsync(progress.CorrelationId, progress.State);

        result.ShouldBe(progress);
    }

    [Fact]
    public async Task WaitCompletesWhenMatchingEventIsPublished()
    {
        using var fixture = new MonitorFixture();
        var progress = CreateProgress(ExecutionState.Cancelled, name: "Job");
        var wait = fixture.Monitor.WaitForStateAsync(ExecutionState.Cancelled, name: "Job");

        fixture.Reporter.Publish(CreateProgress(ExecutionState.Completed));
        fixture.Reporter.Publish(progress);

        (await wait).ShouldBe(progress);
    }

    [Fact]
    public async Task CountWaitReturnsMatchingEventsInPublicationOrder()
    {
        using var fixture = new MonitorFixture();
        var first = CreateProgress(ExecutionState.Completed);
        var second = CreateProgress(ExecutionState.Completed);
        var wait = fixture.Monitor.WaitForCountAsync(ExecutionState.Completed, 2);

        fixture.Reporter.Publish(first);
        fixture.Reporter.Publish(CreateProgress(ExecutionState.Running));
        fixture.Reporter.Publish(second);

        (await wait).ShouldBe([first, second]);
    }

    [Fact]
    public async Task ConcurrentWaitersObserveTheSameEvent()
    {
        using var fixture = new MonitorFixture();
        var progress = CreateProgress(ExecutionState.Expired);
        var firstWait = fixture.Monitor.WaitForStateAsync(progress.CorrelationId, progress.State);
        var secondWait = fixture.Monitor.WaitForStateAsync(progress.CorrelationId, progress.State);

        fixture.Reporter.Publish(progress);

        (await firstWait).ShouldBe(progress);
        (await secondWait).ShouldBe(progress);
    }

    [Fact]
    public async Task CancellationStopsAPendingWait()
    {
        using var cancellation = new CancellationTokenSource();
        using var fixture = new MonitorFixture(cancellationToken: cancellation.Token);
        var wait = fixture.Monitor.WaitForCountAsync(ExecutionState.Completed, 1);

        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(wait);
    }

    [Fact]
    public async Task DisposalStopsAPendingWait()
    {
        var fixture = new MonitorFixture();
        var wait = fixture.Monitor.WaitForCountAsync(ExecutionState.Completed, 1);

        fixture.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(wait);
    }

    [Fact]
    public async Task TimeoutIncludesTheWaitConditionAndRecentProgress()
    {
        using var fixture = new MonitorFixture(waitTimeout: TimeSpan.FromMilliseconds(10));
        fixture.Reporter.Publish(CreateProgress(ExecutionState.Running, name: "ObservedJob"));

        var exception = await Should.ThrowAsync<TimeoutException>(
            fixture.Monitor.WaitForCountAsync(ExecutionState.Completed, 1));

        exception.Message.ShouldContain("1 progress event(s) in state Completed");
        exception.Message.ShouldContain("ObservedJob Running");
    }

    private static ExecutionProgress CreateProgress(ExecutionState state, string? name = null) =>
        new(
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            state,
            Guid.NewGuid(),
            null,
            name,
            typeof(DummyJob),
            true);

    private sealed class MonitorFixture : IDisposable
    {
        private readonly ServiceProvider serviceProvider;

        public MonitorFixture(
            TimeSpan? waitTimeout = null,
            CancellationToken cancellationToken = default)
        {
            Reporter = new TestProgressReporter();
            serviceProvider = new ServiceCollection()
                .AddSingleton<IJobExecutionProgressReporter>(Reporter)
                .BuildServiceProvider();
            Monitor = new ExecutionProgressMonitor(serviceProvider, cancellationToken, waitTimeout);
        }

        public TestProgressReporter Reporter { get; }
        public ExecutionProgressMonitor Monitor { get; }

        public void Dispose()
        {
            Monitor.Dispose();
            serviceProvider.Dispose();
        }
    }

    private sealed class TestProgressReporter : IJobExecutionProgressReporter
    {
        private readonly List<Action<ExecutionProgress>> callbacks = [];

        public IDisposable Register(Action<ExecutionProgress> callback)
        {
            callbacks.Add(callback);
            return new JobExecutionProgressObserver.ActionDisposer(() => callbacks.Remove(callback));
        }

        public void Publish(ExecutionProgress progress)
        {
            foreach (var callback in callbacks.ToArray())
            {
                callback(progress);
            }
        }
    }
}
