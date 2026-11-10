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
    public void DisposingTheFirstSubscriptionOnlyStopsThatSubscriber()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        var firstReceived = 0;
        var secondReceived = 0;
        var firstSubscription = observer.Register(_ => firstReceived++);
        using var secondSubscription = observer.Register(_ => secondReceived++);

        firstSubscription.Dispose();
        _ = CreateRun(observer);

        firstReceived.ShouldBe(0);
        secondReceived.ShouldBe(2);
    }

    [Fact]
    public void DisposingTheLastSubscriptionKeepsEarlierSubscribers()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        var firstReceived = 0;
        var secondReceived = 0;
        using var firstSubscription = observer.Register(_ => firstReceived++);
        var secondSubscription = observer.Register(_ => secondReceived++);

        secondSubscription.Dispose();
        _ = CreateRun(observer);

        firstReceived.ShouldBe(2);
        secondReceived.ShouldBe(0);
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

    [Fact]
    public async Task RootFinalStateIsReportedBeforeConcurrentDependentCompletesOrchestration()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        var received = new List<ExecutionProgress>();
        var root = CreateRun(observer);
        var dependent = root.CreateDependent(JobDefinition.CreateTyped(typeof(DummyJob), parameter: null), null, CancellationToken.None);
        root.NotifyStateChange(JobStateType.Running);
        dependent.NotifyStateChange(JobStateType.Running);
        Task? dependentCompletion = null;

        using var subscription = observer.Register(progress =>
        {
            if (progress.RunId == root.JobRunId && progress.State == ExecutionState.Faulted)
            {
                dependentCompletion = Task.Run(() => dependent.NotifyStateChange(JobStateType.Completed));
                dependentCompletion.Wait(TimeSpan.FromMilliseconds(200));
            }

            lock (received)
            {
                received.Add(progress);
            }
        });

        root.NotifyStateChange(JobStateType.Faulted, new InvalidOperationException());
        await dependentCompletion.ShouldNotBeNull();

        received.Count(p => p.State == ExecutionState.OrchestrationCompleted).ShouldBe(1);
        received[^1].State.ShouldBe(ExecutionState.OrchestrationCompleted);
    }

    [Fact]
    public void JobProgressCarriesRunParameterAndTriggerButOrchestrationProgressDoesNot()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        var received = new List<ExecutionProgress>();
        using var subscription = observer.Register(received.Add);
        var definition = JobDefinition.CreateTyped(typeof(DummyJob), parameter: "configured");

        _ = JobRun.CreateInstant(
            TimeProvider.System,
            observer.Report,
            definition,
            DateTimeOffset.UtcNow,
            OptionalParameter.FromValue("override"),
            CancellationToken.None);

        var orchestration = received.Single(p => p.State == ExecutionState.OrchestrationStarted);
        orchestration.Parameter.ShouldBeNull();
        orchestration.TriggerType.ShouldBeNull();
        orchestration.JobDefinition.ShouldBeNull();
        var job = received.Single(p => p.State == ExecutionState.NotStarted);
        job.Parameter.ShouldBe("override");
        job.TriggerType.ShouldBe(TriggerType.Instant);
        job.JobDefinition.ShouldBeSameAs(definition);
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
