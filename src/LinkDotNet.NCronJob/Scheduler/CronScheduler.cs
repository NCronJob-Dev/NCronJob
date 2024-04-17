using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;

namespace LinkDotNet.NCronJob;

internal sealed partial class CronScheduler : BackgroundService
{
    private readonly JobExecutor jobExecutor;
    private readonly CronRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CronScheduler> logger;
    private readonly SemaphoreSlim semaphore;
    private CancellationTokenSource? shutdown;
    private readonly PriorityQueue<RegistryEntry, DateTimeOffset> jobQueue = new();
    private readonly int maxDegreeOfParallelism;

    public CronScheduler(
        JobExecutor jobExecutor,
        CronRegistry registry,
        TimeProvider timeProvider,
        ILogger<CronScheduler> logger,
        ConcurrencyConfig concurrencyConfig,
        IHostApplicationLifetime lifetime)
    {
        this.jobExecutor = jobExecutor;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.maxDegreeOfParallelism = concurrencyConfig.MaxDegreeOfParallelism;
        this.semaphore = new SemaphoreSlim(concurrencyConfig.MaxDegreeOfParallelism);

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

                if (jobQueue.TryPeek(out var nextJob, out var nextRunTime) && runningTasks.Count < maxDegreeOfParallelism)
                {
                    var delay = nextRunTime - DateTimeOffset.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        if (!TryGetMilliseconds(delay, out var ms))
                        {
                            throw new InvalidOperationException("The delay is too long to be handled by Task.Delay.");
                        }
                        await Task.Delay(TimeSpan.FromMilliseconds(ms), timeProvider, stopToken);
                    }

                    stopToken.ThrowIfCancellationRequested();

                    jobQueue.Dequeue();
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
                        }
                    }, stopToken);

                    runningTasks.Add(task);
                    ScheduleJob(nextJob);
                }

                // Wait for at least one task to complete if there are no available slots
                if (runningTasks.Count >= maxDegreeOfParallelism)
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
        var nextRunTime = job.CronExpression!.GetNextOccurrence(DateTimeOffset.Now, job.TimeZone);
        if (nextRunTime.HasValue)
        {
            jobQueue.Enqueue(job, nextRunTime.Value);
        }
    }

    private async Task ExecuteJob(RegistryEntry entry, CancellationToken stoppingToken)
    {
        try
        {
            LogRunningJob(entry.Type);

            await jobExecutor.RunJob(entry, stoppingToken);   // <-- we should await all the way through so that we can catch exceptions and cancel the job if needed

            LogCompletedJob(entry.Type);
        }
        catch (Exception ex)
        {
            LogExceptionInJob(ex.Message, entry.Type);
        }
    }

    public override void Dispose()
    {
        shutdown?.Dispose();
        semaphore.Dispose();
        base.Dispose();
    }
}
