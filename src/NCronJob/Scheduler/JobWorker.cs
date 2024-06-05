using System.Collections.Concurrent;

namespace NCronJob;
internal sealed class JobWorker
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobProcessor jobProcessor;
    private readonly JobRegistry registry;
    private readonly TimeProvider timeProvider;
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
        ConcurrencySettings concurrencySettings)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobProcessor = jobProcessor;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.globalConcurrencyLimit = concurrencySettings.MaxDegreeOfParallelism;

        taskFactory = TaskFactoryProvider.GetTaskFactory();
    }

    public async Task WorkerAsync(string jobType, CancellationToken cancellationToken)
    {
        var jobQueue = jobQueueManager.GetOrAddQueue(jobType);
        var concurrencyLimit = registry.GetJobTypeConcurrencyLimit(jobType);
        var semaphore = jobQueueManager.GetOrAddSemaphore(jobType, concurrencyLimit);

        while (!cancellationToken.IsCancellationRequested)
        {
            await semaphore.WaitAsync(cancellationToken);

            if (jobQueue.TryPeek(out var nextJob, out var priorityTuple))
            {
                try
                {
                    var cts = jobQueueManager.GetOrAddCancellationTokenSource(jobType);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                    await WaitForNextExecution(priorityTuple, linkedCts.Token);

                    if (IsJobEligibleToStart(nextJob, jobQueue))
                    {
                        jobQueue.Dequeue();
                        UpdateRunningJobCount(nextJob.JobDefinition.JobFullName, 1);

                        _ = taskFactory.StartNew(async () => await jobProcessor.ProcessJobAsync(nextJob, semaphore, cancellationToken), cancellationToken)
                            .Unwrap()
                        .ContinueWith(task =>
                        {
                            UpdateRunningJobCount(nextJob.JobDefinition.JobFullName, -1);

                            if (task.IsFaulted)
                            {
                                nextJob.JobDefinition.NotifyStateChange(new JobState(JobStateType.Failed));
                            }

                            if (task.IsCanceled)
                            {
                                nextJob.JobDefinition.NotifyStateChange(new JobState(JobStateType.Cancelled));
                            }

                        }, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current)
                            .ConfigureAwait(false);

                        if (!nextJob.IsOneTimeJob)
                        {
                            ScheduleJob(nextJob.JobDefinition);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    nextJob.JobDefinition.NotifyStateChange(new JobState(JobStateType.Cancelled));
                    break;
                }
                catch (OperationCanceledException)
                {
                    nextJob.JobDefinition.NotifyStateChange(new JobState(JobStateType.Failed));
                }
            }
            else
            {
                semaphore.Release();
                await Task.Delay(100, cancellationToken); // Avoid tight loop
            }
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
            //    LogNextJobRun(job.Type, nextRunTime.Value.LocalDateTime);  // todo: log by subscribing to OnStateChanged => JobStateType.Scheduled
            var run = JobRun.Create(job);
            run.RunAt = nextRunTime;
            var jobQueue = jobQueueManager.GetOrAddQueue(job.JobFullName);
            jobQueue.Enqueue(run, (nextRunTime.Value, (int)run.Priority));
            job.NotifyStateChange(new JobState(JobStateType.Scheduled));
        }
    }
}
