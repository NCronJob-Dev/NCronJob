using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a background service that schedules jobs based on a cron expression.
/// </summary>
internal sealed class CronScheduler : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly CronRegistry registry;
    private readonly TimeProvider timeProvider;

    public CronScheduler(
        IServiceProvider serviceProvider,
        CronRegistry registry,
        TimeProvider timeProvider)
    {
        this.serviceProvider = serviceProvider;
        this.registry = registry;
        this.timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var tickTimer = new PeriodicTimer(TimeSpan.FromSeconds(1), timeProvider);

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

        foreach (var instant in registry.GetAllInstantJobs())
        {
            AddJobRuns(runMap, [timeProvider.GetUtcNow().DateTime], instant.Type);
        }

        foreach (var cron in registry.GetAllCronJobs())
        {
            var utcNow = DateTime.UtcNow;
            var runDates = cron.CrontabSchedule.GetNextOccurrences(utcNow, utcNow.AddMinutes(1));
            if (runDates is not null)
            {
                AddJobRuns(runMap, runDates, cron.Type);
            }
        }

        return runMap;
    }

    private static void AddJobRuns(Dictionary<DateTime, List<Type>> runMap, IEnumerable<DateTime> runDates, Type type)
    {
        foreach (var runDate in runDates)
        {
            if (runMap.TryGetValue(runDate, out var value))
            {
                value.Add(type);
            }
            else
            {
                runMap[runDate] = [type];
            }
        }
    }

    private DateTime UtcNowMinutePrecision()
    {
        var now = timeProvider.GetUtcNow();
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
    }
}
