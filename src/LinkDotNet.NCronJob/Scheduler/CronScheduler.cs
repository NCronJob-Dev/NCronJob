using System.Diagnostics.CodeAnalysis;
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
    private readonly NCronJobOptions options;

    public CronScheduler(
        IServiceProvider serviceProvider,
        CronRegistry registry,
        TimeProvider timeProvider,
        NCronJobOptions options)
    {
        this.serviceProvider = serviceProvider;
        this.registry = registry;
        this.timeProvider = timeProvider;
        this.options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var tickTimer = new PeriodicTimer(options.TimerInterval, timeProvider);

        var runs = new List<Run>();
        while (await tickTimer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            RunActiveJobs(runs, stoppingToken);
            runs = GetNextJobRuns();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tasks are not awaited.")]
    private void RunActiveJobs(List<Run> runs, CancellationToken stoppingToken)
    {
        foreach (var run in runs)
        {
            var scope = serviceProvider.CreateScope();
            var job = (IJob)scope.ServiceProvider.GetRequiredService(run.Type);

            // We don't want to await jobs explicitly because that
            // could interfere with other job runs)
            var jobTask = run.IsolationLevel == IsolationLevel.None
                ? job.Run(run.Context, stoppingToken)
                : Task.Run(() => job.Run(run.Context, stoppingToken), stoppingToken);

            jobTask.ContinueWith(_ => scope.Dispose(), TaskScheduler.Current).ConfigureAwait(false);
        }
    }

    private List<Run> GetNextJobRuns()
    {
        var entries = registry.GetAllInstantJobsAndClear()
            .Select(instant => (Run)instant)
            .ToList();

        var utcNow = timeProvider.GetUtcNow().DateTime;
        foreach (var cron in registry.GetAllCronJobs())
        {
            var runDate = cron.CrontabSchedule!.GetNextOccurrence(utcNow);
            if (runDate <= utcNow.Add(options.TimerInterval))
            {
                entries.Add((Run)cron);
            }
        }

        return entries;
    }

    private record struct Run(Type Type, JobExecutionContext Context, IsolationLevel IsolationLevel)
    {
        public static explicit operator Run(RegistryEntry entry) => new(entry.Type, entry.Context, entry.IsolationLevel);
    }
}
