using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// Keeps track of the running worker task of each queue and lets callers wait until a worker is gone.
/// </summary>
internal sealed class WorkerTaskRegistry
{
    private readonly Dictionary<string, Task> tasks = [];
    private readonly AsyncSignal changed = new();
    private readonly SyncLock gate = new();
    private bool isClosed;

    /// <summary>
    /// Starts and registers a worker unless one is already registered for the queue or <paramref name="isStartingBlocked"/> says no more may start.
    /// </summary>
    public bool TryStart<TState>(
        string queueName,
        TState state,
        Func<TState, bool> isStartingBlocked,
        Func<TState, Task> startWorker,
        [NotNullWhen(true)] out Task? workerTask)
    {
        lock (gate)
        {
            if (isStartingBlocked(state) || tasks.ContainsKey(queueName))
            {
                workerTask = null;
                return false;
            }

            workerTask = startWorker(state);
            tasks[queueName] = workerTask;
            changed.Pulse();
            return true;
        }
    }

    public void Remove(string queueName, Task workerTask)
    {
        lock (gate)
        {
            if (tasks.TryGetValue(queueName, out var currentTask) && currentTask == workerTask)
            {
                tasks.Remove(queueName);
                changed.Pulse();
            }
        }
    }

    public IReadOnlyCollection<string> GetQueueNames()
    {
        lock (gate)
        {
            return [.. tasks.Keys];
        }
    }

    public Task[] GetTasks()
    {
        lock (gate)
        {
            return [.. tasks.Values];
        }
    }

    public async Task WaitForRemovalAsync(string queueName, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task changeNotification;
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(isClosed, this);

                if (!tasks.ContainsKey(queueName))
                {
                    return;
                }

                changeNotification = changed.WaitAsync();
            }

            await changeNotification.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Close()
    {
        lock (gate)
        {
            if (isClosed)
            {
                return;
            }

            isClosed = true;
            changed.Pulse();
        }
    }
}
