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

        var runMap = new Dictionary<DateTime, List<Run>>();
        while (await tickTimer.WaitForNextTickAsync(stoppingToken))
        {
            var now = UtcNowMinutePrecision();
            RunActiveJobs(runMap, now, stoppingToken);
            runMap = GetJobRuns();
        }
    }

    private void RunActiveJobs(Dictionary<DateTime, List<Run>> runMap, DateTime now, CancellationToken stoppingToken)
    {
        if (!runMap.TryGetValue(now, out var currentRuns))
        {
            return;
        }

        foreach (var run in currentRuns)
        {
            var job = (IJob)serviceProvider.GetRequiredService(run.Type);

            // We don't want to await jobs explicitly because that
            // could interfere with other job runs
            _ = job.Run(run.Context, stoppingToken);
        }
    }

    private Dictionary<DateTime, List<Run>> GetJobRuns()
    {
        var runMap = new Dictionary<DateTime, List<Run>>();

        foreach (var instant in registry.GetAllInstantJobs())
        {
            AddJobRuns(runMap, [timeProvider.GetUtcNow().DateTime], instant);
        }

        foreach (var cron in registry.GetAllCronJobs())
        {
            var utcNow = timeProvider.GetUtcNow().DateTime;
            var runDates = cron.CrontabSchedule.GetNextOccurrences(utcNow, utcNow.AddMinutes(1));
            AddJobRuns(runMap, runDates, cron);
        }

        return runMap;
    }

    private static void AddJobRuns(
        Dictionary<DateTime, List<Run>> runMap,
        IEnumerable<DateTime> runDates,
        RegistryEntry entry)
    {
        foreach (var runDate in runDates)
        {
            var run = new Run(entry.Type, entry.Context);
            if (runMap.TryGetValue(runDate, out var value))
            {
                value.Add(run);
            }
            else
            {
                runMap[runDate] = [run];
            }
        }
    }

    private DateTime UtcNowMinutePrecision()
    {
        var now = timeProvider.GetUtcNow().DateTime;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
    }

    private record struct Run(Type Type, JobExecutionContext Context);
}
