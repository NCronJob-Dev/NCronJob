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

        while (await tickTimer.WaitForNextTickAsync(stoppingToken))
        {
            var runs = GetJobRuns();
            RunActiveJobs(runs, stoppingToken);
        }
    }

    private void RunActiveJobs(List<Run> runs, CancellationToken stoppingToken)
    {
        foreach (var run in runs)
        {
            var job = (IJob)serviceProvider.GetRequiredService(run.Type);

            // We don't want to await jobs explicitly because that
            // could interfere with other job runs
            _ = job.Run(run.Context, stoppingToken);
        }
    }

    private List<Run> GetJobRuns()
    {
        var entries = registry.GetAllInstantJobsAndClear()
            .Select(instant => new Run(instant.Type, instant.Context))
            .ToList();

        var utcNow = timeProvider.GetUtcNow().DateTime;
        foreach (var cron in registry.GetAllCronJobs())
        {
            var runDates = cron.CrontabSchedule.GetNextOccurrences(utcNow, utcNow.AddSeconds(1)).ToArray();
            if (runDates.Length > 0)
            {
                entries.Add(new Run(cron.Type, new JobExecutionContext(cron.Context.Parameter)));
            }
        }

        return entries;
    }

    private record struct Run(Type Type, JobExecutionContext Context);
}
