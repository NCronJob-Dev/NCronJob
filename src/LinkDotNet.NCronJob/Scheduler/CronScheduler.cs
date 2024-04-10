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
    private readonly NCronJobOptions options;
    private readonly ILogger<CronScheduler> logger;

    public CronScheduler(
        JobExecutor jobExecutor,
        CronRegistry registry,
        TimeProvider timeProvider,
        NCronJobOptions options,
        ILogger<CronScheduler> logger)
    {
        this.jobExecutor = jobExecutor;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var tickTimer = new PeriodicTimer(options.TimerInterval, timeProvider);

        var runs = new List<RegistryEntry>();
        while (await tickTimer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            jobExecutor.RunActiveJobs(runs, stoppingToken);
            runs = GetNextJobRuns();
        }
    }

    private List<RegistryEntry> GetNextJobRuns()
    {
        LogBeginGetNextJobRuns();
        var entries = registry.GetAllInstantJobsAndClear().ToList();

        LogInstantJobRuns(entries.Count);

        var utcNow = timeProvider.GetUtcNow().DateTime;
        foreach (var cron in registry.GetAllCronJobs())
        {
            LogCronJobGetsScheduled(cron.Type);
            var runDate = cron.CrontabSchedule!.GetNextOccurrence(utcNow);
            if (runDate <= utcNow.Add(options.TimerInterval))
            {
                LogNextJobRun(cron.Type, runDate);
                entries.Add(cron);
            }
        }

        LogEndGetNextJobRuns();
        return entries;
    }
}
