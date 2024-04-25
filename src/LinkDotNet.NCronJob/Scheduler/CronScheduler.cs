using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
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
        this.logger = loggerFactory.CreateLogger<CronScheduler>();
        this.globalConcurrencyLimit = concurrencySettings.MaxDegreeOfParallelism;
        this.semaphore = new SemaphoreSlim(concurrencySettings.MaxDegreeOfParallelism);

        lifetime.ApplicationStopping.Register(() => this.shutdown?.Cancel());
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);
        var runningTasks = new List<Task>();

        ScheduleInitialJobs();

        try
        {
            while (!stopToken.IsCancellationRequested && jobQueue.Count > 0)
            {
                // Remove completed or canceled tasks from the list to avoid memory overflow
                runningTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

                if (jobQueue.TryPeek(out var nextJob, out var priorityTuple))
                {
                    var utcNow = timeProvider.GetUtcNow();
                    var delay = priorityTuple.NextRunTime - utcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        if (!TryGetMilliseconds(delay, out var ms))
                        {
                            throw new InvalidOperationException("The delay is too long to be handled by Task.Delay.");
                        }
                        await Task.Delay(TimeSpan.FromMilliseconds(ms), timeProvider, stopToken);
                    }

                    if (stopToken.IsCancellationRequested)
                        break;

                    // Recheck the queue to confirm that the next job is still the correct job to execute
                    if (jobQueue.TryPeek(out var confirmedNextJob, out _)
                        && confirmedNextJob == nextJob
                        && CanStartJob(nextJob)
                        && runningTasks.Count < globalConcurrencyLimit)
                    {
                        jobQueue.Dequeue();
                        UpdateRunningJobCount(nextJob.Type, 1);

                        await semaphore.WaitAsync(stopToken);
                        var task = Task.Run(async () =>
                        {
                            try
                            {
                                await ExecuteJob(nextJob, stopToken);
                            }
                            finally
                            {
                                semaphore.Release();
                                UpdateRunningJobCount(nextJob.Type, -1);  // Decrement count when job is done
                            }
                        }, stopToken);

                        runningTasks.Add(task);
                        ScheduleJob(nextJob); // Reschedule immediately after execution
                    }
                }

                // Wait for at least one task to complete if there are no available slots
                if (runningTasks.Count >= globalConcurrencyLimit)
                {
                    // This prevents starting new jobs until at least one current job finishes
                    await Task.WhenAny(runningTasks);
                }
            }

            // Wait for all remaining tasks to complete
            await Task.WhenAll(runningTasks);
        }
        catch (OperationCanceledException)
        {
            LogCancellationOperationInJob();
        }
    }

    /// <summary>
    /// Extracts the milliseconds from a <see cref="TimeSpan"/> value.
    /// Makes sure the value is within the supported range for what Task.Delay can handle, which is about 49ish hours.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="milliseconds"></param>
    /// <returns></returns>
    private static bool TryGetMilliseconds(TimeSpan value, out uint milliseconds)
    {
        const UInt32 maxSupportedTimeout = 0xfffffffe;
        var ms = (long)value.TotalMilliseconds;
        if (ms is >= 1 and <= maxSupportedTimeout || value == Timeout.InfiniteTimeSpan)
        {
            milliseconds = (uint)ms;
            return true;
        }

        milliseconds = 0;
        return false;
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


    private async Task ExecuteJob(RegistryEntry entry, CancellationToken stoppingToken)
    {
        try
        {
            LogRunningJob(entry.Type);

            await jobExecutor.RunJob(entry, stoppingToken);

            LogCompletedJob(entry.Type);
        }
        catch (Exception ex)
        {
            LogExceptionInJob(ex.Message, entry.Type);
        }
    }

    private bool CanStartJob(RegistryEntry jobEntry)
    {
        var attribute = jobEntry.Type.GetCustomAttribute<SupportsConcurrencyAttribute>();
        var maxAllowed = attribute?.MaxDegreeOfParallelism ?? 1; // Default to 1 if no attribute is found
        var currentCount = runningJobCounts.GetOrAdd(jobEntry.Type, _ => 0);

        return currentCount < maxAllowed;
    }

    private void UpdateRunningJobCount(Type jobType, int change) =>
        runningJobCounts.AddOrUpdate(jobType, change, (type, existingVal) => Math.Max(0, existingVal + change));

    public override void Dispose()
    {
        shutdown?.Dispose();
        semaphore.Dispose();
        base.Dispose();
    }
}
