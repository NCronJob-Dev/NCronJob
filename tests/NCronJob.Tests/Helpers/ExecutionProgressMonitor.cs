using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob.Tests;

public sealed class ExecutionProgressMonitor : IDisposable
{
    private readonly object sync = new();
    private readonly List<ExecutionProgress> events = [];
    private readonly Dictionary<Guid, ExecutionState> latestStateByRunId = [];
    private readonly IDisposable subscription;
    private readonly CancellationToken cancellationToken;
    private readonly TimeSpan waitTimeout;
    private TaskCompletionSource eventsChanged = CreateSignal();
    private bool disposed;

    public ExecutionProgressMonitor(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        TimeSpan? waitTimeout = null)
    {
        this.cancellationToken = cancellationToken;
        this.waitTimeout = waitTimeout ?? TimeSpan.FromSeconds(10);
        subscription = serviceProvider
            .GetRequiredService<IJobExecutionProgressReporter>()
            .Register(Report);
    }

    public IList<ExecutionProgress> Events
    {
        get
        {
            lock (sync)
            {
                return [.. events];
            }
        }
    }

    internal bool HasActiveRuns
    {
        get
        {
            lock (sync)
            {
                return latestStateByRunId.Values.Any(IsActive);
            }
        }
    }

    internal Task WaitForChangeAsync()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return eventsChanged.Task;
        }
    }

    public Task<IList<ExecutionProgress>> WaitForCountAsync(ExecutionState state, int count) =>
        WaitForCountAsync(progress => progress.State == state, count, $"{count} progress event(s) in state {state}");

    public async Task<IList<ExecutionProgress>> WaitForCountAsync(
        Func<ExecutionProgress, bool> predicate,
        int count,
        string description)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var startedAt = Stopwatch.GetTimestamp();

        while (true)
        {
            Task changed;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                var matches = events.Where(predicate).Take(count).ToList();
                if (matches.Count == count)
                {
                    return matches;
                }

                changed = eventsChanged.Task;
            }

            var remaining = waitTimeout - Stopwatch.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(BuildTimeoutMessage(description));
            }

            try
            {
                await changed.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                throw new TimeoutException(BuildTimeoutMessage(description), exception);
            }
        }
    }

    public async Task<ExecutionProgress> WaitForStateAsync(Guid correlationId, ExecutionState state)
    {
        var matches = await WaitForCountAsync(
            progress => progress.CorrelationId == correlationId && progress.State == state,
            1,
            $"state {state} for orchestration {correlationId}");

        return matches[0];
    }

    public async Task<ExecutionProgress> WaitForStateAsync(
        ExecutionState state,
        string? name = null,
        Type? type = null)
    {
        var matches = await WaitForCountAsync(
            progress => progress.State == state
                && (name is null || progress.Name == name)
                && (type is null || progress.Type == type),
            1,
            $"state {state} for job name '{name ?? "*"}' and type '{type?.FullName ?? "*"}'");

        return matches[0];
    }

    public void Dispose()
    {
        TaskCompletionSource signal;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            signal = eventsChanged;
            eventsChanged = CreateSignal();
        }

        subscription.Dispose();
        signal.TrySetResult();
    }

    private string BuildTimeoutMessage(string description)
    {
        var history = Events;
        var recentEvents = history.Count == 0
            ? "<none>"
            : string.Join(
                Environment.NewLine,
                history.TakeLast(20).Select(progress =>
                    $"{progress.Timestamp:o} {progress.CorrelationId} {progress.Name ?? progress.Type?.Name ?? "orchestration"} {progress.State}"));

        return $"Timed out waiting for {description}.{Environment.NewLine}Recent progress:{Environment.NewLine}{recentEvents}";
    }

    private void Report(ExecutionProgress progress)
    {
        TaskCompletionSource signal;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            events.Add(progress);
            if (progress.RunId is { } runId)
            {
                latestStateByRunId[runId] = progress.State;
            }

            signal = eventsChanged;
            eventsChanged = CreateSignal();
        }

        signal.TrySetResult();
    }

    private static bool IsActive(ExecutionState state) =>
        state is ExecutionState.Initializing
            or ExecutionState.Running
            or ExecutionState.Retrying
            or ExecutionState.Completing
            or ExecutionState.WaitingForDependency;

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
