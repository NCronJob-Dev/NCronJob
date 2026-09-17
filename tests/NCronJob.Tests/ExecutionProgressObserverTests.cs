using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace NCronJob.Tests;

public class ExecutionProgressObserverTests
{
    [Fact]
    public async Task SubscriptionDisposalIsThreadSafeAndIdempotent()
    {
        var invocationCount = 0;
        using var subscription = new JobExecutionProgressObserver.ActionDisposer(
            () => Interlocked.Increment(ref invocationCount));

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(subscription.Dispose)));

        invocationCount.ShouldBe(1);
    }

    [Fact]
    public void ThrowingSubscriberDoesNotBlockOtherSubscribers()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        var received = new List<ExecutionProgress>();
        using var throwingSubscription = observer.Register(_ => throw new InvalidOperationException("Subscriber failed."));
        using var recordingSubscription = observer.Register(received.Add);

        _ = CreateRun(observer);

        received.Count.ShouldBe(2);
        received.Select(progress => progress.State).ShouldBe(
            [ExecutionState.OrchestrationStarted, ExecutionState.NotStarted]);
    }

    [Fact]
    public async Task ConcurrentRegistrationReportingAndDisposalIsSafe()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        var received = 0;

        await Task.WhenAll(Enumerable.Range(0, 100).Select(index => Task.Run(() =>
        {
            using var subscription = observer.Register(_ => Interlocked.Increment(ref received));
            _ = CreateRun(observer);
        })));

        received.ShouldBeGreaterThan(0);
    }

    private static JobRun CreateRun(JobExecutionProgressObserver observer) =>
        JobRun.CreateInstant(
            TimeProvider.System,
            observer.Report,
            JobDefinition.CreateTyped(typeof(DummyJob), parameter: null),
            DateTimeOffset.UtcNow,
            OptionalParameter.Unspecified,
            CancellationToken.None);
}
