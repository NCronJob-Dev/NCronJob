using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace LinkDotNet.NCronJob;

internal sealed partial class CronScheduler : BackgroundService
{
    private readonly JobExecutor jobExecutor;
    private readonly CronRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CronScheduler> logger;
    private readonly SemaphoreSlim semaphore;
    private CancellationTokenSource? shutdown;
    private readonly PriorityQueue<RegistryEntry, DateTime> jobQueue = new();

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
        this.semaphore = new SemaphoreSlim(concurrencyConfig.MaxDegreeOfParallelism);

        lifetime.ApplicationStopping.Register(() => this.shutdown?.Cancel());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);

        // Schedule initial jobs
        ScheduleInitialJobs();

        try
        {
            while (!stopToken.IsCancellationRequested && jobQueue.Count > 0)
            {
                if (jobQueue.TryPeek(out var nextJob, out var nextRunTime))
                {
                    var delay = nextRunTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, timeProvider, stopToken);
                    }

                    stopToken.ThrowIfCancellationRequested();

                    jobQueue.Dequeue();
                    await semaphore.WaitAsync(stopToken);
                    _ = Task.Run(async () =>
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

                    // Reschedule immediately after run
                    ScheduleJob(nextJob);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
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
        var nextRunTime = job.CrontabSchedule!.GetNextOccurrence(DateTime.UtcNow);
        jobQueue.Enqueue(job, nextRunTime);
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
