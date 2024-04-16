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
        var tasks = registry.GetAllCronJobs().Select(c => ScheduleJobAsync(c, stoppingToken));
        return Task.WhenAll(tasks);
    }

    private async Task ScheduleJobAsync(RegistryEntry entry, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = timeProvider.GetUtcNow().DateTime;
            var runDate = entry.CrontabSchedule!.GetNextOccurrence(now);
            LogNextJobRun(entry.Type, runDate);

            var delay = runDate - now;
            await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            jobExecutor.RunJob(entry, stoppingToken);
        }
    }
}
