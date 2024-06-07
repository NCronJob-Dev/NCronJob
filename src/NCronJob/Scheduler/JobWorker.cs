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

    public async Task WorkerAsync(string jobType, CancellationToken cancellationToken)
    {
        var jobQueue = jobQueueManager.GetOrAddQueue(jobType);
        var concurrencyLimit = registry.GetJobTypeConcurrencyLimit(jobType);
        var semaphore = jobQueueManager.GetOrAddSemaphore(jobType, concurrencyLimit);
        var runningTasks = new List<Task>();

        while (!cancellationToken.IsCancellationRequested)
        {
            runningTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

            if (jobQueue.TryPeek(out var nextJob, out var priorityTuple) && IsJobEligibleToStart(nextJob, jobQueue))
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                await DispatchJobForProcessing(nextJob, priorityTuple, jobType, semaphore, runningTasks, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Avoid tight loop when there's no job queued
                await Task.WhenAny(runningTasks.Concat([Task.Delay(500, cancellationToken)])).ConfigureAwait(false);
            }
        }

        await Task.WhenAll(runningTasks).ConfigureAwait(false);
    }

    private async Task DispatchJobForProcessing(
    JobRun nextJob,
    (DateTimeOffset NextRunTime, int Priority) priorityTuple,
    string jobType,
    SemaphoreSlim semaphore,
    List<Task> runningTasks,
    CancellationToken cancellationToken)
    {
        try
        {
            var cts = jobQueueManager.GetOrAddCancellationTokenSource(jobType);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            var linkedToken = linkedCts.Token;

            await WaitForNextExecution(priorityTuple, linkedCts.Token).ConfigureAwait(false);

            if(jobQueueManager.TryGetQueue(jobType, out var jobQueue))
               jobQueue.Dequeue();
            else
                throw new InvalidOperationException($"Job queue not found for {jobType}");

            UpdateRunningJobCount(nextJob.JobDefinition.JobFullName, 1);

            var jobTask = taskFactory.StartNew(async () =>
            {
                await jobProcessor.ProcessJobAsync(nextJob, linkedToken).ConfigureAwait(false);
            }, cancellationToken).Unwrap().ContinueWith(task =>
            {
                UpdateRunningJobCount(nextJob.JobDefinition.JobFullName, -1);

                if (task.IsFaulted)
                {
                    nextJob.NotifyStateChange(JobStateType.Faulted);
                }

                if (task.IsCanceled)
                {
                    nextJob.NotifyStateChange(JobStateType.Cancelled);
                }

            }, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);

            runningTasks.Add(jobTask);

            if (!nextJob.IsOneTimeJob)
            {
                ScheduleJob(nextJob.JobDefinition);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            nextJob.NotifyStateChange(JobStateType.Cancelled);
        }
        catch (OperationCanceledException)
        {
            nextJob.NotifyStateChange(JobStateType.Faulted);
        }
        catch (Exception ex)
        {
            LogExceptionInJob(ex.Message, nextJob.JobDefinition.Type);
            nextJob.NotifyStateChange(JobStateType.Faulted, ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task WaitForNextExecution((DateTimeOffset NextRunTime, int Priority) priorityTuple, CancellationToken stopToken)
    {
        var utcNow = timeProvider.GetUtcNow();
        var delay = priorityTuple.NextRunTime - utcNow;
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
        var nextRunTime = job.CronExpression!.GetNextOccurrence(utcNow, job.TimeZone);

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
}
