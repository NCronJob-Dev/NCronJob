using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a background service that schedules jobs based on a cron expression.
/// </summary>
internal sealed class CronScheduler : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IReadOnlyCollection<CronRegistryEntry> cronJobs;

    public CronScheduler(
        IServiceProvider serviceProvider,
        IEnumerable<CronRegistryEntry> cronJobs)
    {
        this.serviceProvider = serviceProvider;
        this.cronJobs = cronJobs.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var tickTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        var runMap = new Dictionary<DateTime, List<Type>>();
        while (await tickTimer.WaitForNextTickAsync(stoppingToken))
        {
            var now = UtcNowMinutePrecision();
            RunActiveJobs(runMap, now, stoppingToken);
            runMap = GetJobRuns();
        }
    }

    private void RunActiveJobs(Dictionary<DateTime, List<Type>> runMap, DateTime now, CancellationToken stoppingToken)
    {
        if (!runMap.TryGetValue(now, out var currentRuns))
        {
            return;
        }

        foreach (var job in currentRuns.Select(run => (IJob)serviceProvider.GetRequiredService(run)))
        {
            // We don't want to await jobs explicitly because that
            // could interfere with other job runs
            _ = job.Run(stoppingToken);
        }
    }

    private Dictionary<DateTime, List<Type>> GetJobRuns()
    {
        var runMap = new Dictionary<DateTime, List<Type>>();
        foreach (var cron in cronJobs)
        {
            var utcNow = DateTime.UtcNow;
            var runDates = cron.CrontabSchedule.GetNextOccurrences(utcNow, utcNow.AddMinutes(1));
            if (runDates is not null)
            {
                AddJobRuns(runMap, runDates, cron);
            }
        }

        return runMap;
    }

    private static void AddJobRuns(Dictionary<DateTime, List<Type>> runMap, IEnumerable<DateTime> runDates, CronRegistryEntry cron)
    {
        foreach (var runDate in runDates)
        {
            if (runMap.TryGetValue(runDate, out var value))
            {
                value.Add(cron.Type);
            }
            else
            {
                runMap[runDate] = [cron.Type];
            }
        }
    }

    private static DateTime UtcNowMinutePrecision()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
    }
}
