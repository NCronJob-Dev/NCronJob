using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace LinkDotNet.NCronJob;

internal sealed partial class CronScheduler : BackgroundService
{
    private readonly JobExecutor jobExecutor;
    private readonly CronRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CronScheduler> logger;
    private readonly SemaphoreSlim semaphore;
    private CancellationTokenSource? shutdown;
    private readonly PriorityQueue<RegistryEntry, (DateTimeOffset NextRunTime, int Priority)> jobQueue =
        new(new JobQueueTupleComparer());
    private readonly int globalConcurrencyLimit;
    private readonly ConcurrentDictionary<Type, int> runningJobCounts = [];


    public CronScheduler(
        JobExecutor jobExecutor,
        CronRegistry registry,
        TimeProvider timeProvider,
        ConcurrencySettings concurrencySettings,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime)
    {
        this.jobExecutor = jobExecutor;
        this.registry = registry;
        this.timeProvider = timeProvider;
        logger = loggerFactory.CreateLogger<CronScheduler>();
        globalConcurrencyLimit = concurrencySettings.MaxDegreeOfParallelism;
        semaphore = new SemaphoreSlim(concurrencySettings.MaxDegreeOfParallelism);

        lifetime.ApplicationStopping.Register(() => shutdown?.Cancel());
    }

    public override void Dispose()
    {
        shutdown?.Dispose();
        semaphore.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        shutdown?.Dispose();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);
        var runningTasks = new List<Task>();

        ScheduleInitialJobs();

        try
        {
            while (!stopToken.IsCancellationRequested && jobQueue.Count > 0)
            {
                runningTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

                if (jobQueue.TryPeek(out var nextJob, out var priorityTuple))
                {
                    await WaitForNextExecution(priorityTuple, stopToken);

                    if (stopToken.IsCancellationRequested)
                        break;

                    if (IsJobEligableToStart(nextJob, runningTasks))
                    {
                        jobQueue.Dequeue();
                        UpdateRunningJobCount(nextJob.Type, 1);

                        await semaphore.WaitAsync(stopToken);
                        var task = CreateExecutionTask(nextJob, stopToken);

                        runningTasks.Add(task);
                        ScheduleJob(nextJob);
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

    private void ScheduleInitialJobs()
    {
        foreach (var job in registry.GetAllCronJobs())
        {
            ScheduleJob(job);
        }
    }

    private void ScheduleJob(RegistryEntry job)
    {
        var utcNow = timeProvider.GetUtcNow();
        var nextRunTime = job.CronExpression!.GetNextOccurrence(utcNow, job.TimeZone);

        if (nextRunTime.HasValue)
        {
            LogNextJobRun(job.Type, nextRunTime.Value.LocalDateTime);
            // higher means more priority
            var priorityValue = (int)job.Priority;
            jobQueue.Enqueue(job, (nextRunTime.Value, priorityValue));
        }
    }

    private async Task WaitForNextExecution((DateTimeOffset NextRunTime, int Priority) priorityTuple, CancellationToken stopToken)
    {
        var utcNow = timeProvider.GetUtcNow().DateTime;
        var delay = priorityTuple.NextRunTime - utcNow;
        if (delay > TimeSpan.Zero)
        {
            await TaskExtensions.LongDelaySafe(delay, timeProvider, stopToken);
        }
    }

    private Task CreateExecutionTask(RegistryEntry nextJob, CancellationToken stopToken)
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
                UpdateRunningJobCount(nextJob.Type, -1);
            }
        }, stopToken);
        return task;
    }

    private bool IsJobEligableToStart(RegistryEntry nextJob, List<Task> runningTasks)
    {
        var isSameJob = jobQueue.TryPeek(out var confirmedNextJob, out _) && confirmedNextJob == nextJob;
        var concurrentSlotsOpen = runningTasks.Count < globalConcurrencyLimit;
        return isSameJob && CanStartJob(nextJob) && concurrentSlotsOpen;
    }

    private async Task ExecuteJob(RegistryEntry entry, CancellationToken stoppingToken)
    {
        try
        {
            LogRunningJob(entry.Type);

            await jobExecutor.RunJob(entry, stoppingToken).ConfigureAwait(false);

            LogCompletedJob(entry.Type);
        }
        catch (Exception ex)
        {
            LogExceptionInJob(ex.Message, entry.Type);
        }
        finally
        {
            entry.IncrementJobExecutionCount();
        }
    }

    private bool CanStartJob(RegistryEntry jobEntry)
    {
        var attribute = jobEntry.Type.GetCustomAttribute<SupportsConcurrencyAttribute>();
        var maxAllowed = attribute?.MaxDegreeOfParallelism ?? 1;
        var currentCount = runningJobCounts.GetOrAdd(jobEntry.Type, _ => 0);

        return currentCount < maxAllowed;
    }

    private void UpdateRunningJobCount(Type jobType, int change) =>
        runningJobCounts.AddOrUpdate(jobType, change, (_, existingVal) => Math.Max(0, existingVal + change));
}
