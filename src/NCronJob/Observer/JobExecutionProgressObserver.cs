using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutionProgressObserver : IJobExecutionProgressReporter
{
    private readonly ILogger<JobExecutionProgressObserver> logger;

    public JobExecutionProgressObserver(ILogger<JobExecutionProgressObserver> logger)
    {
        this.logger = logger;
    }

    private readonly SyncLock subscribersLock = new();

    private Action<ExecutionProgress>[] subscribers = [];

    public IDisposable Register(Action<ExecutionProgress> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (subscribersLock)
        {
            subscribers = [.. subscribers, callback];
        }

        return new ActionDisposer(() => Unregister(callback));
    }

    private void Unregister(Action<ExecutionProgress> callback)
    {
        lock (subscribersLock)
        {
            var index = Array.IndexOf(subscribers, callback);
            if (index < 0)
            {
                return;
            }

            subscribers = [.. subscribers[..index], .. subscribers[(index + 1)..]];
        }
    }

    private static ExecutionProgress ToOrchestrationProgress(ExecutionProgress progress, ExecutionState state) =>
        progress with
        {
            State = state,
            RunId = null,
            ParentRunId = null,
            Name = null,
            Type = null,
            IsTypedJob = null,
        };

    internal void Report(JobRun run)
    {
        if (Volatile.Read(ref subscribers).Length == 0)
        {
            return;
        }

        var progress = run.ToExecutionProgress();

        if (run.IsOrchestrationRoot && progress.State == ExecutionState.NotStarted)
        {
            Dispatch(ToOrchestrationProgress(progress, ExecutionState.OrchestrationStarted));
            Dispatch(progress);
        }
        else if (run.IsCompleted && run.RootJobIsCompleted)
        {
            Dispatch(progress);
            Dispatch(ToOrchestrationProgress(progress, ExecutionState.OrchestrationCompleted));
        }
        else
        {
            Dispatch(progress);
        }
    }

    private void Dispatch(ExecutionProgress progress)
    {
        foreach (var callback in Volatile.Read(ref subscribers))
        {
            try
            {
                callback(progress);
            }
            catch (Exception ex)
            {
                LogCallbackFailed(progress.State, ex);
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "An execution progress callback threw while reporting state '{State}'.")]
    private partial void LogCallbackFailed(ExecutionState state, Exception exception);

    internal sealed class ActionDisposer : IDisposable
    {
        private Action? disposer;

        public ActionDisposer(Action disposer)
        {
            this.disposer = disposer;
        }

        public void Dispose() => Interlocked.Exchange(ref disposer, null)?.Invoke();
    }
}
