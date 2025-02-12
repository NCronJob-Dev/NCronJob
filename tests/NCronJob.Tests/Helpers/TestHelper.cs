using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public abstract class JobIntegrationBase : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource
        = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? serviceProvider;

    private readonly TaskCompletionSource<bool> cancellationSignaled = new();
    protected Task CancellationSignaled => cancellationSignaled.Task;
    protected CancellationToken CancellationToken => cancellationTokenSource.Token;
    protected ServiceCollection ServiceCollection { get; }
    protected FakeTimeProvider FakeTimer { get; } = new() { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };
    protected Storage Storage { get; }

    protected JobIntegrationBase()
    {
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

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        cancellationSignaled.TrySetCanceled();
        serviceProvider?.Dispose();

        var current = TestContext.Current;

        if (current.Warnings is not null)
        {
            current.TestOutputHelper!.WriteLine("** Warnings:");

            foreach (string warning in current.Warnings)
            {
                current.TestOutputHelper!.WriteLine(warning);
            }
        }

        if (current.TestState is not null && current.TestState.Result == TestResult.Failed)
        {
            current.TestOutputHelper!.WriteLine("** Storage:");

            foreach ((string timestamp, string content) in Storage.TimedEntries)
            {
                current.TestOutputHelper!.WriteLine($"{timestamp} {content}");
            }
        }
    }

    protected ServiceProvider ServiceProvider => serviceProvider ??= ServiceCollection.BuildServiceProvider();

    protected async Task<IList<ExecutionProgress>> WaitForNthOrchestrationState(
        IList<ExecutionProgress> events,
        ExecutionState state,
        int howMany,
        Action? onAnyFoundButLast = null)
    {
        List<ExecutionProgress> seen = new();

        await WaitUntilConditionIsMet(events, LastOfNthCompletedOrchestration);

        return seen;

        ExecutionProgress? LastOfNthCompletedOrchestration(IList<ExecutionProgress> executionProgresses)
        {
            var matches = executionProgresses.Where(e => e.State == state).ToList();

            if (onAnyFoundButLast is not null && matches.Count > 0 && matches.Count < howMany)
            {
                onAnyFoundButLast();
            }

            if (matches.Count < howMany)
            {
                return null;
            }

            seen.AddRange(matches.Take(howMany));

            return seen.Last();
        }
    }

    protected async Task WaitForOrchestrationCompletion(
        IList<ExecutionProgress> events,
        Guid orchestrationId)
    {
        await WaitForOrchestrationState(events, orchestrationId, ExecutionState.OrchestrationCompleted);
    }

    protected async Task WaitForOrchestrationState(
        IList<ExecutionProgress> events,
        Guid orchestrationId,
        ExecutionState state)
    {
        await WaitUntilConditionIsMet(events, OrchestrationHasReachedExpectedState);

        ExecutionProgress? OrchestrationHasReachedExpectedState(IList<ExecutionProgress> events)
        {
            return events.FirstOrDefault(@event =>
                @event.CorrelationId == orchestrationId &&
                @event.State == state);
        }
    }

    protected static (IDisposable subscription, IList<ExecutionProgress> events) RegisterAnExecutionProgressSubscriber(IServiceProvider serviceProvider)
    {
        SynchronizedCollection<ExecutionProgress> events = [];

        void Subscriber(ExecutionProgress progress)
        {
            events.Add(progress);
        }

        var progressReporter = serviceProvider.GetRequiredService<IJobExecutionProgressReporter>();

        return (progressReporter.Register(Subscriber), events);
    }

    private async Task<ExecutionProgress> WaitUntilConditionIsMet(
        IList<ExecutionProgress> events,
        Func<IList<ExecutionProgress>, ExecutionProgress?> evaluator)
    {
        // Note: Although this function could seem a bit over-engineered, it's sadly necessary.
        // Indeed, events may actually be updated upstream while it's being enumerated
        // in here (which leads to a "Collection was modified; enumeration operation may not
        // execute." error message would we using any enumerating based (eg. Linq) traversal.

        int index = 0;

        List<ExecutionProgress> tmp = [];

        while (true)
        {
            int count = events.Count;

            while (index < count)
            {
                tmp.Add(events[index]);
                index++;
            }

            var entry = evaluator(tmp);

            if (entry is not null)
            {
                return entry;
            }

            FakeTimer.Advance(TimeSpan.FromSeconds(1));

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken);
        }
    }
}

public sealed class Storage(TimeProvider timeProvider)
{
#if NET9_0_OR_GREATER
    private readonly Lock locker = new();
#else
    private readonly object locker = new();
#endif
    public IList<string> Entries => new ReadOnlyCollection<string>(TimedEntries.Select(e => e.Item2).ToList());
    public IList<(string, string)> TimedEntries { get; } = [];

    public void Add(string content)
    {
        lock (locker)
        {
            TimedEntries.Add((timeProvider.GetUtcNow().ToString("o"), content));
        }
    }
}
