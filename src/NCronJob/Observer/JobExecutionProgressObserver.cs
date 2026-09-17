using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutionProgressObserver : IJobExecutionProgressReporter
{
    private readonly ILogger<JobExecutionProgressObserver> logger;
    private readonly List<Action<ExecutionProgress>> callbacks = [];

    public JobExecutionProgressObserver(ILogger<JobExecutionProgressObserver> logger)
    {
        this.logger = logger;
    }

#if NET9_0_OR_GREATER
    private readonly Lock callbacksLock = new();
#else
    private readonly object callbacksLock = new();
#endif

    public IDisposable Register(Action<ExecutionProgress> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (callbacksLock)
        {
            callbacks.Add(callback);
        }

        return new ActionDisposer(() =>
        {
            lock (callbacksLock)
            {
                callbacks.Remove(callback);
            }
        });
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
        List<ExecutionProgress> progresses = [];

        var progress = run.ToExecutionProgress();
        progresses.Add(progress);

        if (run.IsOrchestrationRoot && progress.State == ExecutionState.NotStarted)
        {
            var orchestrationStarted = ToOrchestrationProgress(progress, ExecutionState.OrchestrationStarted);

            progresses.Insert(0, orchestrationStarted);
        }
        else if (run.IsCompleted && run.RootJobIsCompleted)
        {
            var orchestrationCompleted = ToOrchestrationProgress(progress, ExecutionState.OrchestrationCompleted);

            progresses.Add(orchestrationCompleted);
        }

        // Take a snapshot of callbacks while holding the lock to avoid race conditions
        Action<ExecutionProgress>[] callbacksSnapshot;
        lock (callbacksLock)
        {
            callbacksSnapshot = callbacks.ToArray();
        }

        foreach (var callback in callbacksSnapshot)
        {
            foreach (var entry in progresses)
            {
                try
                {
                    callback(entry);
                }
                catch (Exception ex)
                {
                    LogCallbackFailed(entry.State, ex);
                }
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "An execution progress callback threw while reporting state '{State}'.")]
    private partial void LogCallbackFailed(ExecutionState state, Exception exception);

    internal sealed class ActionDisposer : IDisposable
    {
        private bool disposed;
        private readonly Action disposer;

        public ActionDisposer(Action disposer)
        {
            this.disposer = disposer;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposer();

            disposed = true;
        }
    }
}
