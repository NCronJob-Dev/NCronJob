using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NCronJob;

internal sealed partial class QueueWorker : BackgroundService
{
    private readonly JobExecutor jobExecutor;
    private readonly JobRegistry registry;
    private readonly JobQueue jobQueue;
    private readonly StartupJobManager startupJobManager;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<QueueWorker> logger;
    private readonly SemaphoreSlim semaphore;
    private readonly int globalConcurrencyLimit;
    private readonly SemaphoreSlim queueWaiter = new(0);
    private readonly ConcurrentDictionary<string, int> runningJobCounts = [];
    private CancellationTokenSource? shutdown;
    private CancellationTokenSource rescheduleTrigger = new();

    public QueueWorker(
        JobExecutor jobExecutor,
        JobRegistry registry,
        JobQueue jobQueue,
        StartupJobManager startupJobManager,
        TimeProvider timeProvider,
        ConcurrencySettings concurrencySettings,
        ILogger<QueueWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        this.jobExecutor = jobExecutor;
        this.registry = registry;
        this.jobQueue = jobQueue;
        this.startupJobManager = startupJobManager;
        this.timeProvider = timeProvider;
        this.logger = logger;
        globalConcurrencyLimit = concurrencySettings.MaxDegreeOfParallelism;
        semaphore = new SemaphoreSlim(concurrencySettings.MaxDegreeOfParallelism);

        lifetime.ApplicationStopping.Register(() => shutdown?.Cancel());
        this.jobQueue.JobQueueChanged += RescheduleAllJobs;
    }

    public override void Dispose()
    {
        shutdown?.Dispose();
        semaphore.Dispose();
        queueWaiter.Dispose();
        rescheduleTrigger.Dispose();
        jobQueue.JobQueueChanged -= RescheduleAllJobs;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        shutdown?.Dispose();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);
        var runningTasks = new List<Task>();

        await startupJobManager.ProcessStartupJobs(CreateExecutionTask, stopToken);

        ScheduleCronJobs();

        try
        {
            await startupJobManager.WaitForStartupJobsCompletion();

            while (!stopToken.IsCancellationRequested)
            {
                runningTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

                if (jobQueue.Count == 0)
                {
                    await queueWaiter.WaitAsync(stopToken);
                    continue;
                }

                if (jobQueue.TryPeek(out var nextJob, out var priorityTuple))
                {
                    await WaitForNextExecution(priorityTuple, stopToken);

                    if (stopToken.IsCancellationRequested)
                        break;

                    if (rescheduleTrigger.IsCancellationRequested)
                    {
                        rescheduleTrigger.Dispose();
                        rescheduleTrigger = new();
                        continue;
                    }

                    if (IsJobEligibleToStart(nextJob, runningTasks))
                    {
                        jobQueue.Dequeue();
                        UpdateRunningJobCount(nextJob.JobDefinition.JobFullName, 1);

                        await semaphore.WaitAsync(stopToken);
                        var task = CreateExecutionTask(nextJob, stopToken);

                        runningTasks.Add(task);
                        ScheduleJob(nextJob.JobDefinition);
                    }
                    else
                    {
                        // Note: do not remove, this is used to reduce the CPU usage for special cases dealing
                        // with concurrent threads, otherwise the loop will run as fast as possible when the max concurrency limit is reached
                        // while it waits for the tasks to complete
                        await Task.Delay(1, stopToken);
                    }
                }

                if (runningTasks.Count >= globalConcurrencyLimit)
                {
                    await Task.WhenAny(runningTasks);
                }
            }

            await Task.WhenAll(runningTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogCancellationOperationInJob();
        }
    }

    private void RescheduleAllJobs(object? sender, EventArgs e)
    {
        jobQueue.RemoveAllCron();
        ScheduleCronJobs();
        queueWaiter.Release();
        rescheduleTrigger.Cancel();
    }

    private void ScheduleCronJobs()
    {
        foreach (var job in registry.GetAllCronJobs())
        {
            ScheduleJob(job);
        }
    }

    private void ScheduleJob(JobDefinition job)
    {
        if (job.CronExpression is null)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow();
        var nextRunTime = job.CronExpression!.GetNextOccurrence(utcNow, job.TimeZone);

        if (nextRunTime.HasValue)
        {
            LogNextJobRun(job.Type, nextRunTime.Value.LocalDateTime);
            // higher means more priority
            var run = JobRun.Create(job);
            jobQueue.Enqueue(run, (nextRunTime.Value, (int)run.Priority));
        }
    }

    private async Task WaitForNextExecution((DateTimeOffset NextRunTime, int Priority) priorityTuple,
        CancellationToken stopToken)
    {
        var utcNow = timeProvider.GetUtcNow();
        var delay = priorityTuple.NextRunTime - utcNow;
        if (delay > TimeSpan.Zero)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopToken, rescheduleTrigger.Token);
            try
            {
                await TaskExtensions.LongDelaySafe(delay, timeProvider, tokenSource.Token);
            }
            catch (OperationCanceledException) when (rescheduleTrigger.IsCancellationRequested)
            {
                // ignore as we need to reevaluate the queue
            }
        }
    }

    private Task CreateExecutionTask(JobRun nextJob, CancellationToken stopToken)
    {
        var task = Task.Run(async () =>
        {
            try
            {
                await ExecuteJob(nextJob, stopToken);
            }
            finally
            {
                semaphore.Release();
                UpdateRunningJobCount(nextJob.JobDefinition.JobFullName, -1);
            }
        }, stopToken);
        return task;
    }

    private bool IsJobEligibleToStart(JobRun nextJob, List<Task> runningTasks)
    {
        var isSameJob = jobQueue.TryPeek(out var confirmedNextJob, out _) && confirmedNextJob == nextJob;
        var concurrentSlotsOpen = runningTasks.Count < globalConcurrencyLimit;
        return isSameJob && CanStartJob(nextJob.JobDefinition) && concurrentSlotsOpen;
    }

    private async Task ExecuteJob(JobRun entry, CancellationToken stoppingToken)
    {
        var type = entry.JobDefinition.Type;
        try
        {
            LogRunningJob(type);

            await jobExecutor.RunJob(entry, stoppingToken).ConfigureAwait(false);

            LogCompletedJob(type);
        }
        catch (Exception ex)
        {
            LogExceptionInJob(ex.Message, type);
        }
        finally
        {
            entry.IncrementJobExecutionCount();
        }
    }

    private bool CanStartJob(JobDefinition jobEntry)
    {
        var maxAllowed = jobEntry.ConcurrencyPolicy?.MaxDegreeOfParallelism ?? 1;
        var currentCount = runningJobCounts.GetOrAdd(jobEntry.JobFullName, _ => 0);

        return currentCount < maxAllowed;
    }

    private void UpdateRunningJobCount(string jobFullName, int change) =>
        runningJobCounts.AddOrUpdate(jobFullName, change, (_, existingVal) => Math.Max(0, existingVal + change));
}
