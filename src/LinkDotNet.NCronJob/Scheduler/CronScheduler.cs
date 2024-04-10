using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a background service that schedules jobs based on a cron expression.
/// </summary>
internal sealed partial class CronScheduler : BackgroundService
{
    private readonly JobExecutor jobExecutor;
    private readonly CronRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CronScheduler> logger;

    public CronScheduler(
        JobExecutor jobExecutor,
        CronRegistry registry,
        TimeProvider timeProvider,
        ILogger<CronScheduler> logger)
    {
        this.jobExecutor = jobExecutor;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var cron in registry.GetAllCronJobs())
        {
            ScheduleJob(cron, stoppingToken);
        }

        return Task.CompletedTask;
    }

    private void ScheduleJob(RegistryEntry entry, CancellationToken stoppingToken)
    {
        var now = timeProvider.GetUtcNow().DateTime;
        var runDate = entry.CrontabSchedule!.GetNextOccurrence(now);
        LogNextJobRun(entry.Type, runDate);
        var delay = runDate - now;
        _ = Task.Delay(delay, timeProvider, stoppingToken)
            .ContinueWith(_ =>
                    RunAndRescheduleJob(entry, stoppingToken),
                stoppingToken,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default)
            .ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    private void RunAndRescheduleJob(RegistryEntry entry, CancellationToken stoppingToken)
    {
        LogRunningJob(entry.Type);
        jobExecutor.RunJob(entry, stoppingToken);
        ScheduleJob(entry, stoppingToken);
    }
}
