using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobWorker
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobProcessor jobProcessor;
    private readonly CronRunScheduler cronRunScheduler;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<JobWorker> logger;
    private readonly JobConcurrencyLimiter concurrencyLimiter;
    private readonly ConcurrentDictionary<Task, byte> runningJobs = new();

    public JobWorker(
        JobQueueManager jobQueueManager,
        JobProcessor jobProcessor,
        CronRunScheduler cronRunScheduler,
        TimeProvider timeProvider,
        JobConcurrencyLimiter concurrencyLimiter,
        ILogger<JobWorker> logger)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobProcessor = jobProcessor;
        this.cronRunScheduler = cronRunScheduler;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.concurrencyLimiter = concurrencyLimiter;
    }

    public async Task ProcessQueueAsync(string queueName, CancellationToken cancellationToken)
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

                var capacityChanged = concurrencyLimiter.WaitForReleaseAsync();
                if (!concurrencyLimiter.TryAcquire(nextJob.JobDefinition))
                {
                    await Task.WhenAny(queueChanged, capacityChanged).WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!jobQueue.TryDequeueIf(nextJob))
                {
                    concurrencyLimiter.Release(nextJob.JobDefinition);
                    continue;
                }

                if (!await nextJob.WaitForActivationAsync().ConfigureAwait(false))
                {
                    nextJob.NotifyStateChange(JobStateType.Cancelled);
                    concurrencyLimiter.Release(nextJob.JobDefinition);
                    continue;
                }

                _ = StartJobProcessingAsync(nextJob, cancellationToken);

                if (nextJob.TriggerType == TriggerType.Cron)
                {
                    cronRunScheduler.ScheduleNextRun(nextJob.JobDefinition, priority.NextRunTime);
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

    public async Task RunImmediatelyAsync(JobRun jobRun, CancellationToken cancellationToken)
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

        concurrencyLimiter.AcquireIgnoringLimits(jobRun.JobDefinition);
        await StartJobProcessingAsync(jobRun, cancellationToken).ConfigureAwait(false);
    }

    private Task StartJobProcessingAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        Task jobTask;

        // Each run starts from a clean execution context, so it doesn't inherit ambient state (e.g. log scopes) of whoever triggered it.
        using (ExecutionContext.SuppressFlow())
        {
            jobTask = Task.Run(async () =>
            {
                try
                {
                    await jobProcessor.ProcessJobAsync(jobRun, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    concurrencyLimiter.Release(jobRun.JobDefinition);
                }
            }, CancellationToken.None);
        }

        runningJobs.TryAdd(jobTask, 0);
        jobTask.ContinueWith(
            static (completedTask, state) => ((ConcurrentDictionary<Task, byte>)state!).TryRemove(completedTask, out _),
            runningJobs,
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

}
