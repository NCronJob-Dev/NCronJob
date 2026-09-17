using System.Threading;
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
}
