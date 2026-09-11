using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobWorker
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobProcessor jobProcessor;
    private readonly JobRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly JobExecutionProgressObserver observer;
    private readonly ILogger<JobWorker> logger;
    private readonly int globalConcurrencyLimit;
    private readonly Dictionary<string, int> runningJobCounts = [];
    private readonly ConcurrentDictionary<Task, byte> runningJobs = new();
    private int totalRunningJobCount;
    private TaskCompletionSource capacitySignal = CreateSignal();
#if NET9_0_OR_GREATER
    private readonly Lock slotLock = new();
#else
    private readonly object slotLock = new();
#endif

    public JobWorker(
        JobQueueManager jobQueueManager,
        JobProcessor jobProcessor,
        JobRegistry registry,
        TimeProvider timeProvider,
        ConcurrencySettings concurrencySettings,
        JobExecutionProgressObserver observer,
        ILogger<JobWorker> logger)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobProcessor = jobProcessor;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.observer = observer;
        this.logger = logger;
        globalConcurrencyLimit = concurrencySettings.MaxDegreeOfParallelism;
    }

    public async Task WorkerAsync(string queueName, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // The signal must be taken before resolving the queue: if the queue gets replaced in between,
                // the removal completes this signal instead of the worker waiting on the new queue's signal while peeking the old queue.
                var queueChanged = jobQueueManager.WaitForChangeAsync(queueName);

                if (!jobQueueManager.TryGetQueue(queueName, out var jobQueue))
                {
                    break;
                }

                if (!jobQueue.TryPeek(out var nextJob, out var priority))
                {
                    await queueChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (priority.NextRunTime > timeProvider.GetUtcNow())
                {
                    await WaitUntilOrChangeAsync(priority.NextRunTime, queueChanged, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var capacityChanged = GetCapacitySignal();
                if (!TryReserveSlot(nextJob.JobDefinition))
                {
                    await Task.WhenAny(queueChanged, capacityChanged).WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!jobQueue.TryDequeueIf(nextJob))
                {
                    ReleaseSlot(nextJob.JobDefinition);
                    continue;
                }

                _ = StartJobProcessingAsync(nextJob, cancellationToken);

                if (nextJob.TriggerType == TriggerType.Cron)
                {
                    ScheduleJob(nextJob.JobDefinition, priority.NextRunTime);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogWorkerCancelled(queueName);
        }
        catch (ObjectDisposedException) when (jobQueueManager.IsDisposed)
        {
            LogJobQueueManagerDisposed();
        }
    }

    /// <summary>
    /// Completes once all jobs started by this worker, including those of already removed queues, have finished.
    /// </summary>
    public Task WaitForRunningJobsAsync() => Task.WhenAll(runningJobs.Keys);

    public async Task InvokeJob(JobRun jobRun, CancellationToken cancellationToken)
    {
        try
        {
            var delay = jobRun.RunAt - timeProvider.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                await Task.LongDelaySafe(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            jobRun.NotifyStateChange(JobStateType.Cancelled);
            return;
        }

        AcquireSlot(jobRun.JobDefinition);
        await StartJobProcessingAsync(jobRun, cancellationToken).ConfigureAwait(false);
    }

    private Task StartJobProcessingAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        var jobTask = Task.Run(async () =>
        {
            try
            {
                await jobProcessor.ProcessJobAsync(jobRun, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ReleaseSlot(jobRun.JobDefinition);
            }
        }, CancellationToken.None);

        runningJobs.TryAdd(jobTask, 0);
        jobTask.ContinueWith(
            completedTask => runningJobs.TryRemove(completedTask, out _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return jobTask;
    }

    private async Task WaitUntilOrChangeAsync(DateTimeOffset dueTime, Task queueChanged, CancellationToken cancellationToken)
    {
        var delay = dueTime - timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.LongDelaySafe(delay, timeProvider, delayCts.Token);

        // Time may have advanced between computing the delay and arming the timer, which would make the timer fire late.
        var completedTask = timeProvider.GetUtcNow() >= dueTime
            ? null
            : await Task.WhenAny(delayTask, queueChanged).ConfigureAwait(false);

        if (completedTask != delayTask)
        {
            await delayCts.CancelAsync().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool TryReserveSlot(JobDefinition jobDefinition)
    {
        var maxAllowed = jobDefinition.ConcurrencyPolicy?.MaxDegreeOfParallelism ?? 1;

        lock (slotLock)
        {
            runningJobCounts.TryGetValue(jobDefinition.JobFullName, out var currentCount);

            if (currentCount >= maxAllowed || totalRunningJobCount >= globalConcurrencyLimit)
            {
                return false;
            }

            runningJobCounts[jobDefinition.JobFullName] = currentCount + 1;
            totalRunningJobCount++;
            return true;
        }
    }

    private void AcquireSlot(JobDefinition jobDefinition)
    {
        lock (slotLock)
        {
            runningJobCounts.TryGetValue(jobDefinition.JobFullName, out var currentCount);
            runningJobCounts[jobDefinition.JobFullName] = currentCount + 1;
            totalRunningJobCount++;
        }
    }

    private void ReleaseSlot(JobDefinition jobDefinition)
    {
        TaskCompletionSource signal;

        lock (slotLock)
        {
            runningJobCounts.TryGetValue(jobDefinition.JobFullName, out var currentCount);
            runningJobCounts[jobDefinition.JobFullName] = Math.Max(0, currentCount - 1);
            totalRunningJobCount = Math.Max(0, totalRunningJobCount - 1);

            signal = capacitySignal;
            capacitySignal = CreateSignal();
        }

        signal.TrySetResult();
    }

    private Task GetCapacitySignal()
    {
        lock (slotLock)
        {
            return capacitySignal.Task;
        }
    }

    public void ScheduleJob(JobDefinition job, DateTimeOffset? lastScheduledRunTime = null)
    {
        if (!job.IsEnabled)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow();

        // When rescheduling after a job fires, the timer may have triggered slightly
        // before the scheduled time. Using utcNow directly could return the same cron
        // slot again, causing duplicate execution. Using the later of utcNow and the
        // last scheduled run time guarantees we always advance past the fired slot.
        var baseTime = lastScheduledRunTime.HasValue && lastScheduledRunTime.Value > utcNow
            ? lastScheduledRunTime.Value
            : utcNow;
        var nextRunTime = job.GetNextCronOccurrence(baseTime);

        if (!nextRunTime.HasValue)
        {
            return;
        }

        LogNextJobRun(job.Name, nextRunTime.Value);
        var run = JobRun.Create(timeProvider, observer.Report, job, nextRunTime.Value);
        run.NotifyStateChange(JobStateType.Scheduled);

        // Checked atomically with queue removal, so a job removed concurrently isn't brought back by a pending reschedule.
        if (!jobQueueManager.Enqueue(run, () => registry.IsRootJob(job)))
        {
            run.NotifyStateChange(JobStateType.Cancelled);
        }
    }

    public void RemoveJobByName(string jobName)
    {
        RemoveJob(() => registry.RemoveByName(jobName));
    }

    public void RemoveJobByType(Type type)
    {
        RemoveJob(() => registry.RemoveByType(type));
    }

    private void RemoveJob(Func<string?> unregistrator)
    {
        var jobDefinitionFullName = unregistrator();

        if (jobDefinitionFullName is null)
        {
            return;
        }

        jobQueueManager.RemoveQueue(jobDefinitionFullName);
    }

    public void RescheduleJob(JobDefinition jobDefinition)
    {
        ArgumentNullException.ThrowIfNull(jobDefinition);

        jobQueueManager.RemoveQueue(jobDefinition.JobFullName);
        ScheduleJob(jobDefinition);
    }

    private static TaskCompletionSource CreateSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
