using Microsoft.Extensions.Hosting;

namespace NCronJob.Dashboard;

internal sealed class DashboardDataStore : IHostedService, IDisposable
{
    private static readonly TimeSpan ChangeDebounce = TimeSpan.FromMilliseconds(250);
#if NET9_0_OR_GREATER
    private readonly Lock sync = new();
#else
    private readonly object sync = new();
#endif
    private readonly JobExecutionProgressObserver observer;
    private readonly NCronJobDashboardOptions options;
    private readonly Dictionary<Guid, StoredRun> runs = [];
    private readonly LinkedList<Guid> completedRunIds = [];
    private Timer? changedTimer;
    private bool subscribed;
    private bool disposed;

    public DashboardDataStore(JobExecutionProgressObserver observer, NCronJobDashboardOptions options)
    {
        this.observer = observer;
        this.options = options;
    }

    public event Action? Changed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!subscribed)
        {
            observer.RunStateChanged += OnRunStateChanged;
            subscribed = true;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    public IReadOnlyList<DashboardRun> GetRunningRuns()
    {
        lock (sync)
        {
            return runs.Values
                .Where(run => IsRunning(run.State))
                .OrderByDescending(run => run.StateChangedAt)
                .Select(ToSnapshot)
                .ToArray();
        }
    }

    public IReadOnlyList<DashboardRun> GetRecentRuns()
    {
        lock (sync)
        {
            var active = runs.Values.Where(run => !run.IsCompleted).OrderByDescending(run => run.StateChangedAt);
            var completed = completedRunIds.Select(id => runs[id]);
            return active.Concat(completed).Select(ToSnapshot).ToArray();
        }
    }

    public IReadOnlyList<OrchestrationSnapshot> GetOrchestrations()
    {
        lock (sync)
        {
            return runs.Values
                .GroupBy(run => run.CorrelationId)
                .OrderByDescending(group => group.Max(run => run.StateChangedAt))
                .Select(group => new OrchestrationSnapshot(
                    group.Key,
                    group.OrderBy(run => run.StateChangedAt).Select(ToSnapshot).ToArray()))
                .ToArray();
        }
    }

    private void OnRunStateChanged(JobRun run)
    {
        lock (sync)
        {
            if (!runs.TryGetValue(run.JobRunId, out var stored))
            {
                stored = new StoredRun(run);
                runs.Add(run.JobRunId, stored);
            }

            if (stored.IsCompleted)
            {
                return;
            }

            stored.State = run.CurrentState.Type;
            stored.StateChangedAt = run.CurrentState.Timestamp;
            if (run.IsCompleted)
            {
                stored.IsCompleted = true;
                completedRunIds.AddFirst(run.JobRunId);
                TrimHistory();
            }

            changedTimer ??= new Timer(_ => NotifyChanged(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            changedTimer.Change(ChangeDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void TrimHistory()
    {
        var maximum = Math.Max(0, options.MaxHistoryEntries);
        while (completedRunIds.Count > maximum)
        {
            var last = completedRunIds.Last!;
            completedRunIds.RemoveLast();
            runs.Remove(last.Value);
        }
    }

    private void NotifyChanged() => Changed?.Invoke();

    private static bool IsRunning(JobStateType state) => state is
        JobStateType.Initializing or
        JobStateType.Running or
        JobStateType.Retrying or
        JobStateType.Completing or
        JobStateType.WaitingForDependency;

    private static DashboardRun ToSnapshot(StoredRun run) => new(
        run.JobRunId,
        run.CorrelationId,
        run.ParentJobRunId,
        run.JobName,
        run.JobType,
        run.State,
        run.StateChangedAt,
        run.TriggerType,
        run.Parameter.Value);

    private void Unsubscribe()
    {
        if (subscribed)
        {
            observer.RunStateChanged -= OnRunStateChanged;
            subscribed = false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Unsubscribe();
        changedTimer?.Dispose();
        disposed = true;
    }

    private sealed class StoredRun
    {
        public StoredRun(JobRun run)
        {
            JobRunId = run.JobRunId;
            CorrelationId = run.CorrelationId;
            ParentJobRunId = run.ParentJobRunId;
            JobName = run.JobDefinition.CustomName ?? run.JobDefinition.Type?.Name ?? "Anonymous job";
            JobType = run.JobDefinition.Type?.FullName ?? run.JobDefinition.JobFullName;
            State = run.CurrentState.Type;
            StateChangedAt = run.CurrentState.Timestamp;
            TriggerType = run.TriggerType;
            Parameter = new Lazy<string>(() => ParameterSerializer.Serialize(run.Parameter));
        }

        public Guid JobRunId { get; }
        public Guid CorrelationId { get; }
        public Guid? ParentJobRunId { get; }
        public string JobName { get; }
        public string JobType { get; }
        public JobStateType State { get; set; }
        public DateTimeOffset StateChangedAt { get; set; }
        public TriggerType TriggerType { get; }
        public Lazy<string> Parameter { get; }
        public bool IsCompleted { get; set; }
    }
}
