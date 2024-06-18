using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NCronJob;

internal sealed partial class JobWorker
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobProcessor jobProcessor;
    private readonly JobRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<JobWorker> logger;
    private readonly int globalConcurrencyLimit;
    private readonly ConcurrentDictionary<string, int> runningJobCounts = [];
    private int TotalRunningJobCount => runningJobCounts.Values.Sum();
    private readonly TaskFactory taskFactory;

#pragma warning disable CA2008
    public JobWorker(
        JobQueueManager jobQueueManager,
        JobProcessor jobProcessor,
        JobRegistry registry,
        TimeProvider timeProvider,
        ConcurrencySettings concurrencySettings,
        ILogger<JobWorker> logger)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobProcessor = jobProcessor;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.logger = logger;
        globalConcurrencyLimit = concurrencySettings.MaxDegreeOfParallelism;

        taskFactory = TaskFactoryProvider.GetTaskFactory();
    }

    public async Task WorkerAsync(string queueName, CancellationToken cancellationToken)
    {
        var jobQueue = jobQueueManager.GetOrAddQueue(queueName);
        var concurrencyLimit = registry.GetJobTypeConcurrencyLimit(queueName);
        var semaphore = jobQueueManager.GetOrAddSemaphore(queueName, concurrencyLimit);
        var runningTasks = new List<Task>();

        while (!cancellationToken.IsCancellationRequested)
        {
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

    public async Task InvokeJobWithSchedule(JobRun jobRun, CancellationToken cancellationToken)
    {
        jobRun.NotifyStateChange(JobStateType.Scheduled);
        await WaitForNextExecution(jobRun.RunAt ?? DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
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
            var cts = jobQueueManager.GetOrAddCancellationTokenSource(queueName);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            var linkedToken = linkedCts.Token;

            await WaitForNextExecution(nextRunTime, linkedCts.Token).ConfigureAwait(false);

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
            LogExceptionInJob(ex.Message, nextJob.JobDefinition.Type);
            nextJob.NotifyStateChange(JobStateType.Faulted, ex.Message);
        }
        finally
        {
            if (shouldReleaseSemaphore)
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
        }, cancellationToken).Unwrap().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                jobRun.NotifyStateChange(JobStateType.Faulted);
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
        if (job.CronExpression is null)
            return;

        var utcNow = timeProvider.GetUtcNow();
        var nextRunTime = job.CronExpression.GetNextOccurrence(utcNow, job.TimeZone);

        if (nextRunTime.HasValue)
        {
            LogNextJobRun(job.Type, nextRunTime.Value.LocalDateTime);  // todo: log by subscribing to OnStateChanged => JobStateType.Scheduled
            var run = JobRun.Create(job);
            run.RunAt = nextRunTime;
            var jobQueue = jobQueueManager.GetOrAddQueue(job.JobFullName);
            jobQueue.Enqueue(run, (nextRunTime.Value, (int)run.Priority));
            run.NotifyStateChange(JobStateType.Scheduled);
        }
    }

    public void RemoveJobByName(string jobName)
    {
        RemoveJobRunByName(jobName);
        registry.RemoveByName(jobName);
    }

    public void RemoveJobType(Type type)
    {
        var fullName = registry.FindJobDefinition(type)?.JobFullName;
        if (fullName is null)
        {
            return;
        }

        registry.RemoveByType(type);
        jobQueueManager.RemoveQueue(fullName);
    }

    public void RescheduleJobWithJobName(JobDefinition jobDefinition)
    {
        ArgumentNullException.ThrowIfNull(jobDefinition);
        ArgumentNullException.ThrowIfNull(jobDefinition.CustomName);

        RemoveJobRunByName(jobDefinition.CustomName);
        ScheduleJob(jobDefinition);
        jobQueueManager.SignalJobQueue(jobDefinition.JobFullName);
    }

    private void RemoveJobRunByName(string jobName)
    {
        var jobType = registry.FindJobDefinition(jobName);
        if (jobType is null)
        {
            return;
        }

        if (!jobQueueManager.TryGetQueue(jobType.JobFullName, out var jobQueue))
        {
            return;
        }

        jobQueue.RemoveByName(jobName);
    }
}
