using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

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
    private readonly ConcurrentDictionary<string, int> runningJobCounts = [];
    private int TotalRunningJobCount => runningJobCounts.Values.Sum();
    private readonly TaskFactory taskFactory;

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

        taskFactory = TaskFactoryProvider.GetTaskFactory();
    }

    public async Task WorkerAsync(string queueName, CancellationToken cancellationToken)
    {
        var concurrencyLimit = registry.GetJobTypeConcurrencyLimit(queueName);
        var semaphore = jobQueueManager.GetOrAddSemaphore(queueName, concurrencyLimit);
        var runningTasks = new List<Task>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var jobQueue = jobQueueManager.GetOrAddQueue(queueName);

            runningTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

            if (jobQueue.TryPeek(out var nextJob, out var priorityTuple) && IsJobEligibleToStart(nextJob, jobQueue))
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                await DispatchJobForProcessing(nextJob, priorityTuple.NextRunTime, queueName, semaphore, runningTasks, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // Avoid tight loop when there's no job queued
                await Task.WhenAny(runningTasks.Concat([Task.Delay(500, cancellationToken)])).ConfigureAwait(false);
            }
        }

        await Task.WhenAll(runningTasks).ConfigureAwait(false);
    }

    public async Task InvokeJob(JobRun jobRun, CancellationToken cancellationToken)
    {
        await WaitForNextExecution(jobRun.RunAt, cancellationToken).ConfigureAwait(false);
        await StartJobProcessingAsync(jobRun, cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchJobForProcessing(
        JobRun nextJob,
        DateTimeOffset nextRunTime,
        string queueName,
        SemaphoreSlim semaphore,
        List<Task> runningTasks,
        CancellationToken cancellationToken)
    {
        var shouldReleaseSemaphore = true;

        try
        {
            var cts = jobQueueManager.GetCancellationTokenSource(queueName);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            var linkedToken = linkedCts.Token;

            await WaitForNextExecution(nextRunTime, linkedCts.Token).ConfigureAwait(false);

            if (jobQueueManager.IsDisposed || linkedToken.IsCancellationRequested)
            {
                // We will most likely run into this, when the IHostApplicationLifetime is stopped
                LogJobQueueManagerDisposed();
                return;
            }

            if (!jobQueueManager.TryGetQueue(queueName, out var jobQueue))
            {
                throw new InvalidOperationException($"Job queue not found for {queueName}");
            }

            jobQueue.Dequeue();

            var jobTask = StartJobProcessingAsync(nextJob, linkedToken).ContinueWith(_ =>
                semaphore.Release(), cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);

            runningTasks.Add(jobTask);

            if (!nextJob.IsOneTimeJob)
            {
                ScheduleJob(nextJob.JobDefinition);
            }

            shouldReleaseSemaphore = false;
        }
        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested || oce.CancellationToken.IsCancellationRequested)
        {
            nextJob.NotifyStateChange(JobStateType.Cancelled);
        }
        catch (Exception ex)
        {
            LogExceptionInJob(ex.Message, nextJob.JobDefinition.Name);
            nextJob.NotifyStateChange(JobStateType.Faulted, ex);
        }
        finally
        {
            if (shouldReleaseSemaphore && !jobQueueManager.IsDisposed)
            {
                semaphore.Release();
            }
        }
    }

    private async Task StartJobProcessingAsync(JobRun jobRun, CancellationToken cancellationToken) =>
        await taskFactory.StartNew(async () =>
        {
            UpdateRunningJobCount(jobRun.JobDefinition.JobFullName, 1);
            try
            {
                await jobProcessor.ProcessJobAsync(jobRun, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                UpdateRunningJobCount(jobRun.JobDefinition.JobFullName, -1);
            }
        }, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default)
            .Unwrap()
            .ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.Assert(task.Exception is not null);
                jobRun.NotifyStateChange(JobStateType.Faulted, task.Exception);
            }

            if (task.IsCanceled)
            {
                jobRun.NotifyStateChange(JobStateType.Cancelled);
            }
        }, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);

    private async Task WaitForNextExecution(DateTimeOffset nextRunTime, CancellationToken stopToken)
    {
        var utcNow = timeProvider.GetUtcNow();
        var delay = nextRunTime - utcNow;
        if (delay > TimeSpan.Zero)
        {
            await TaskExtensions.LongDelaySafe(delay, timeProvider, stopToken).ConfigureAwait(false);
        }
    }

    private bool IsJobEligibleToStart(JobRun nextJob, JobQueue jobQueue)
    {
        var isSameJob = jobQueue.TryPeek(out var confirmedNextJob, out _) && confirmedNextJob == nextJob;
        var concurrentSlotsOpen = TotalRunningJobCount < globalConcurrencyLimit;
        return isSameJob && CanStartJob(nextJob.JobDefinition) && concurrentSlotsOpen;
    }

    private bool CanStartJob(JobDefinition jobEntry)
    {
        var maxAllowed = jobEntry.ConcurrencyPolicy?.MaxDegreeOfParallelism ?? 1;
        var currentCount = runningJobCounts.GetOrAdd(jobEntry.JobFullName, _ => 0);

        return currentCount < maxAllowed;
    }

    private void UpdateRunningJobCount(string jobFullName, int change) =>
        runningJobCounts.AddOrUpdate(jobFullName, change, (_, existingVal) => Math.Max(0, existingVal + change));

    public void ScheduleJob(JobDefinition job)
    {
        var utcNow = timeProvider.GetUtcNow();
        var nextRunTime = job.GetNextCronOccurrence(utcNow);

        if (!nextRunTime.HasValue)
        {
            return;
        }

        var jobQueue = jobQueueManager.GetOrAddQueue(job.JobFullName);

        LogNextJobRun(job.Name, nextRunTime.Value);  // todo: log by subscribing to OnStateChanged => JobStateType.Scheduled
        var run = JobRun.Create(timeProvider, observer.Report, job, nextRunTime.Value);
        jobQueue.Enqueue(run, (nextRunTime.Value, (int)run.Priority));
        run.NotifyStateChange(JobStateType.Scheduled);
    }

    public void RemoveJobByName(string jobName)
    {
        RemoveJob(() => registry.RemoveByName(jobName));
    }

    public void RemoveJobByType(Type type)
    {
        RemoveJob(() => registry.RemoveByType(type));
    }

    private void RemoveJob(
        Func<string?> unregistrator)
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
        jobQueueManager.SignalJobQueue(jobDefinition.JobFullName);
    }
}
