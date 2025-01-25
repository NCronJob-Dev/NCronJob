using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed class JobExecutionProgressObserver : IJobExecutionProgressReporter
{
    private readonly List<Action<ExecutionProgress>> callbacks = [];
    private readonly TimeProvider timeProvider;

#if NET9_0_OR_GREATER
    private readonly Lock callbacksLock = new();
#else
    private readonly object callbacksLock = new();
#endif

    public JobExecutionProgressObserver(
        TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

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

    internal void Report(JobRun run)
    {
        List<ExecutionProgress> progresses = [];

        var progress = new ExecutionProgress(run, timeProvider.GetUtcNow());
        progresses.Add(progress);

        if (run.IsOrchestrationRoot && progress.State == ExecutionState.NotStarted)
        {
            var orchestrationStarted = progress
            with
            {
                State = ExecutionState.OrchestrationStarted,
                RunId = null,
                ParentRunId = null,
            };

            progresses.Insert(0, orchestrationStarted);
        }
        else if (run.IsCompleted && run.RootJobIsCompleted)
        {
            var orchestrationCompleted = progress
            with
            {
                State = ExecutionState.OrchestrationCompleted,
                RunId = null,
                ParentRunId = null,
            };

            progresses.Add(orchestrationCompleted);
        }

        foreach (var callback in callbacks)
        {
            foreach (var entry in progresses)
            {
                callback(entry);
            }
        }
    }

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
