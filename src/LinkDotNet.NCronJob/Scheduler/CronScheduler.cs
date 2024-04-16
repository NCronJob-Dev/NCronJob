using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LinkDotNet.NCronJob;

internal record ConcurrencyConfig(int MaxDegreeOfParallelism, bool AllowConcurrency);

/// <summary>
/// Represents a background service that schedules jobs based on a cron expression.
/// </summary>
internal sealed partial class CronScheduler : BackgroundService
{
    private readonly JobExecutor jobExecutor;
    private readonly CronRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CronScheduler> logger;
    private readonly int maxDegreeOfParallelism;
    private readonly bool allowConcurrency;
    private readonly ConcurrentDictionary<DateTime, List<RegistryEntry>> nextRunTimes = new();
    private readonly SemaphoreSlim semaphore;
    private CancellationTokenSource? shutdown;

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
        this.allowConcurrency = concurrencyConfig.AllowConcurrency;
        this.semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

        lifetime.ApplicationStopping.Register(() => this.shutdown?.Cancel());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;

        stopToken.Register(() => Console.WriteLine("Cancellation requested for CronScheduler from stopToken."));

        var allTasks = new List<Task>();

        try
        {
            foreach (var job in registry.GetAllCronJobs())
            {
                var now = timeProvider.GetUtcNow().DateTime;
                var nextRunTime = job.CrontabSchedule!.GetNextOccurrence(now);
                nextRunTimes.AddOrUpdate(nextRunTime, [job],
                    (key, existingList) =>
                    {
                        existingList.Add(job);
                        return existingList;
                    });
            }

            while (!stopToken.IsCancellationRequested && !nextRunTimes.IsEmpty)
            {
                var nextTaskTime = nextRunTimes.Keys.Min();
                var jobsToRun = nextRunTimes[nextTaskTime];
                nextRunTimes.TryRemove(nextTaskTime, out _);

                var delay = nextTaskTime - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, timeProvider, stopToken);
                }

                stopToken.ThrowIfCancellationRequested();

                foreach (var job in jobsToRun)
                {
                    if (stopToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await semaphore.WaitAsync(stopToken);
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteJob(job, stopToken);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, stopToken);

                    allTasks.Add(task);
                }

                // Optionally wait for all tasks to complete if concurrency is not allowed
                if (!allowConcurrency)
                {
                    await Task.WhenAll(allTasks);
                    allTasks.Clear(); // Clear all tasks after they complete
                }
            }

            // Wait for all tasks before disposing semaphore
            if (allTasks.Count > 0)
            {
                await Task.WhenAll(allTasks);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested
        }
    }

    private async Task ExecuteJob(RegistryEntry entry, CancellationToken stoppingToken)
    {
        try
        {
            LogRunningJob(entry.Type);
            await jobExecutor.RunJob(entry, stoppingToken);   // <-- we should await all the way through so that we can catch exceptions and cancel the job if needed

            if (!stoppingToken.IsCancellationRequested)
            {
                var nextRunTime = entry.CrontabSchedule!.GetNextOccurrence(DateTime.UtcNow);
                nextRunTimes.AddOrUpdate(nextRunTime, [entry],
                    (key, existingList) => { existingList.Add(entry); return existingList; });
            }
        }
        catch (Exception ex)
        {
            LogExceptionInJob(ex.Message, entry.Type);
        }
    }

    public override void Dispose()
    {
        shutdown?.Dispose();
        nextRunTimes.Clear();
        semaphore.Dispose();
        base.Dispose();
    }
}
