using System.Collections.Immutable;

namespace NCronJob.Dashboard;

internal sealed record RunRecord(
    Guid RunId,
    Guid CorrelationId,
    Guid? ParentRunId,
    JobDefinition? Definition,
    string? CustomName,
    Type? JobType,
    TriggerType? TriggerType,
    ParameterText? Parameter,
    ImmutableList<RunStateChange> States)
{
    public string JobName => CustomName ?? JobType?.FullName ?? Definition?.JobFullName ?? "Untyped job";

    public RunStateChange Current => States[^1];

    public DateTimeOffset CreatedAt => States[0].Timestamp;

    public TimeSpan? Duration => States.Find(s => s.State == ExecutionState.Running) is { } started && IsFinished
        ? Current.Timestamp - started.Timestamp
        : null;

    public bool IsFinished => Current.State is ExecutionState.Completed or ExecutionState.Faulted
        or ExecutionState.Cancelled or ExecutionState.Expired or ExecutionState.Skipped;

    public bool BelongsTo(JobDefinition job) => ReferenceEquals(Definition, job);
}

internal sealed record RunStateChange(ExecutionState State, DateTimeOffset Timestamp);

// ponytail: in-memory, lost on restart; persist if history matters
internal sealed class DashboardRunStore : IDisposable
{
    internal const int Capacity = 500;

    private readonly object sync = new();
    private readonly Dictionary<Guid, RunRecord> runs = [];
    private readonly Queue<Guid> evictableFinishedRuns = new();
    private IDisposable? subscription;

    public event Action? Changed;

    public void Subscribe(IJobExecutionProgressReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        subscription?.Dispose();
        subscription = reporter.Register(Record);
    }

    public IReadOnlyList<RunRecord> GetRuns()
    {
        lock (sync)
        {
            return [.. runs.Values.OrderByDescending(r => r.CreatedAt)];
        }
    }

    internal void Record(ExecutionProgress progress)
    {
        if (progress.RunId is not { } runId)
        {
            return;
        }

        var change = new RunStateChange(progress.State, progress.Timestamp);
        var parameter = IsKnown(runId) ? null : JsonParameter.Describe(progress.Parameter);

        lock (sync)
        {
            var existing = runs.GetValueOrDefault(runId);
            var record = existing is null
                ? CreateRecord(runId, progress, change, parameter)
                : existing with { States = existing.States.Add(change) };
            runs[runId] = record;

            if (record.IsFinished && existing?.IsFinished != true)
            {
                KeepBoundedHistory(record);
            }
        }

        Changed?.Invoke();
    }

    // Active runs and startup outcomes are never evicted: the Jobs page needs them, and both are naturally bounded.
    private void KeepBoundedHistory(RunRecord finished)
    {
        if (finished.TriggerType == TriggerType.Startup)
        {
            return;
        }

        evictableFinishedRuns.Enqueue(finished.RunId);
        if (evictableFinishedRuns.Count > Capacity)
        {
            runs.Remove(evictableFinishedRuns.Dequeue());
        }
    }

    // Serializing a parameter can be slow, and progress is reported synchronously by running jobs, so it happens outside the lock.
    private bool IsKnown(Guid runId)
    {
        lock (sync)
        {
            return runs.ContainsKey(runId);
        }
    }

    private static RunRecord CreateRecord(Guid runId, ExecutionProgress progress, RunStateChange change, ParameterText? parameter) =>
        new(
            runId,
            progress.CorrelationId,
            progress.ParentRunId,
            progress.JobDefinition,
            progress.Name,
            progress.Type,
            progress.TriggerType,
            parameter,
            [change]);

    public void Dispose() => subscription?.Dispose();
}
