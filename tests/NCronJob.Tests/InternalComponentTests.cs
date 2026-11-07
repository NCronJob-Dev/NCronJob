using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public sealed class TrackingServiceCollectionTests
{
    [Fact]
    public void RollbackRemovesOnlyTheTrackedDescriptors()
    {
        var existingBefore = ServiceDescriptor.Singleton("existing before");
        IServiceCollection services = new ServiceCollection();
        services.Add(existingBefore);
        var tracking = new TrackingServiceCollection(services);
        var added = ServiceDescriptor.Singleton("added");
        var inserted = ServiceDescriptor.Singleton(new Version(1, 0));
        var existingAfter = ServiceDescriptor.Singleton(new Uri("https://after.example"));

        tracking.Add(added);
        tracking.Insert(0, inserted);
        services.Add(existingAfter);

        tracking.Rollback();

        services.ShouldBe([existingBefore, existingAfter]);
    }

    [Fact]
    public void InsertedDescriptorsAreTrackedAndInsertedAtTheGivenPosition()
    {
        var existing = ServiceDescriptor.Singleton("existing");
        IServiceCollection services = new ServiceCollection();
        services.Add(existing);
        var tracking = new TrackingServiceCollection(services);
        var inserted = ServiceDescriptor.Singleton(new Version(1, 0));

        tracking.Insert(0, inserted);

        services.ShouldBe([inserted, existing]);
        tracking.Rollback();
        services.ShouldBe([existing]);
    }

    [Fact]
    public void RollbackLeavesDescriptorsAlone()
    {
        var existing = ServiceDescriptor.Singleton("existing");
        IServiceCollection services = new ServiceCollection();
        services.Add(existing);
        var tracking = new TrackingServiceCollection(services);

        tracking.Rollback();

        services.ShouldBe([existing]);
    }
}

public sealed class WorkerTaskRegistryTests
{
    [Fact]
    public void StartsAWorkerOncePerQueue()
    {
        var registry = new WorkerTaskRegistry();
        var startCount = 0;
        Task StartWorker(int _)
        {
            startCount++;
            return Task.Delay(Timeout.Infinite);
        }

        registry.TryStart("queue", 0, _ => false, StartWorker, out var first).ShouldBeTrue();
        registry.TryStart("queue", 0, _ => false, StartWorker, out var second).ShouldBeFalse();

        first.ShouldNotBeNull();
        second.ShouldBeNull();
        startCount.ShouldBe(1);
        registry.GetQueueNames().ShouldBe(["queue"]);
    }

    [Fact]
    public void DoesNotStartAWorkerWhenStartingIsBlocked()
    {
        var registry = new WorkerTaskRegistry();
        var started = false;

        registry.TryStart("queue", 0, _ => true, _ =>
        {
            started = true;
            return Task.CompletedTask;
        }, out var worker).ShouldBeFalse();

        worker.ShouldBeNull();
        started.ShouldBeFalse();
        registry.GetQueueNames().ShouldBeEmpty();
    }

    [Fact]
    public void RemoveOnlyForgetsTheRegisteredWorkerTask()
    {
        var registry = new WorkerTaskRegistry();
        registry.TryStart("queue", 0, _ => false, _ => Task.Delay(Timeout.Infinite), out var worker);

        registry.Remove("queue", Task.CompletedTask);
        registry.GetQueueNames().ShouldBe(["queue"]);

        registry.Remove("queue", worker!);
        registry.GetQueueNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task WaitForRemovalCompletesOnceTheWorkerIsRemoved()
    {
        var registry = new WorkerTaskRegistry();
        registry.TryStart("queue", 0, _ => false, _ => Task.Delay(Timeout.Infinite), out var worker);

        var waiting = registry.WaitForRemovalAsync("queue", TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        waiting.IsCompleted.ShouldBeFalse();

        registry.Remove("queue", worker!);

        await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WaitForRemovalCompletesImmediatelyForUnknownQueues()
    {
        var registry = new WorkerTaskRegistry();

        var waiting = registry.WaitForRemovalAsync("unknown", TestContext.Current.CancellationToken);

        await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        waiting.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task ClosingTheRegistryFailsPendingAndFutureWaits()
    {
        var registry = new WorkerTaskRegistry();
        registry.TryStart("queue", 0, _ => false, _ => Task.Delay(Timeout.Infinite), out _);
        var waiting = registry.WaitForRemovalAsync("queue", TestContext.Current.CancellationToken);

        registry.Close();
        registry.Close();

        await Should.ThrowAsync<ObjectDisposedException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ObjectDisposedException>(() => registry.WaitForRemovalAsync("queue", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReturnsTheTasksOfAllWorkers()
    {
        var registry = new WorkerTaskRegistry();
        registry.TryStart("a", 0, _ => false, _ => Task.Delay(Timeout.Infinite), out var a);
        registry.TryStart("b", 0, _ => false, _ => Task.Delay(Timeout.Infinite), out var b);

        registry.GetTasks().ShouldBe([a!, b!], ignoreOrder: true);
    }
}

public sealed class ObservablePriorityQueueTests
{
    [Fact]
    public void TryDequeueIfOnlyRemovesTheExpectedHead()
    {
        var timeProvider = new FakeTimeProvider();
        var queue = new JobQueue("queue");
        var first = CreateRun(timeProvider, timeProvider.GetUtcNow());
        var second = CreateRun(timeProvider, timeProvider.GetUtcNow().AddMinutes(1));
        queue.EnqueueForDirectExecution(first);
        queue.EnqueueForDirectExecution(second);
        var changes = new List<NotifyCollectionChangedAction>();
        queue.CollectionChanged += (_, e) => changes.Add(e.Action);

        queue.TryDequeueIf(second).ShouldBeFalse();
        queue.Count.ShouldBe(2);
        changes.ShouldBeEmpty();

        queue.TryDequeueIf(first).ShouldBeTrue();
        queue.Single().ShouldBeSameAs(second);
        changes.ShouldBe([NotifyCollectionChangedAction.Remove]);
    }

    [Fact]
    public void TryDequeueIfOnAnEmptyQueueFails()
    {
        var timeProvider = new FakeTimeProvider();
        var queue = new JobQueue("queue");

        queue.TryDequeueIf(CreateRun(timeProvider, timeProvider.GetUtcNow())).ShouldBeFalse();
    }

    [Fact]
    public void EnqueueRejectsNullArguments()
    {
        var queue = new ObservablePriorityQueue<string, int>(Comparer<int>.Default);

        Should.Throw<ArgumentNullException>(() => queue.Enqueue(null!, 1));
    }

    [Fact]
    public void ComparerIsRequired()
    {
        Should.Throw<ArgumentNullException>(() => new ObservablePriorityQueue<string, int>(null!));
    }

    private static JobRun CreateRun(TimeProvider timeProvider, DateTimeOffset runAt) =>
        JobRun.CreateCron(timeProvider, _ => { }, JobDefinition.CreateTyped(typeof(DummyJob), null), runAt);
}

public sealed class QueueChangeLoggerTests
{
    [Fact]
    public void UnexpectedCollectionChangesAreRejected()
    {
        using var queueManager = new JobQueueManager();
        using var logger = new QueueChangeLogger(queueManager, NullLogger<QueueChangeLogger>.Instance);
        var method = typeof(QueueChangeLogger).GetMethod(
            "LogQueueChange",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.ShouldNotBeNull();

        var exception = Should.Throw<System.Reflection.TargetInvocationException>(() =>
            method.Invoke(logger, [this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)]));

        var inner = exception.InnerException.ShouldBeOfType<ArgumentOutOfRangeException>();
        inner.Message.ShouldContain("Unexpected collection change action in LogQueueChange");
    }
}

public sealed class RetryPolicyFactoryTests
{
    [Fact]
    public async Task NoRetriesMeansTheActionRunsExactlyOnceWithoutHandlingFailures()
    {
        var policy = RetryPolicyFactory.Create(
            new FakeTimeProvider(),
            maxRetryAttempts: 0,
            _ => TimeSpan.Zero,
            (_, _, _) => throw new InvalidOperationException("Must not retry."));
        var executions = 0;

        await Should.ThrowAsync<ApplicationException>(() => policy.ExecuteAsync(() =>
        {
            executions++;
            throw new ApplicationException();
#pragma warning disable CS0162
            return Task.CompletedTask;
#pragma warning restore CS0162
        }));

        executions.ShouldBe(1);
    }

    [Fact]
    public void UnsupportedPolicyTypesAreRejected()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var factory = new PolicyCreatorFactory(serviceProvider);

        var exception = Should.Throw<ArgumentException>(() => factory.CreatePolicy((PolicyType)42, 1, 1));

        exception.Message.ShouldBe("Unsupported policy type");
    }
}
