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
        while (await tickTimer.WaitForNextTickAsync(stoppingToken))
        {
            RunActiveJobs(runs, stoppingToken);
            runs = GetNextJobRuns();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tasks are not awaited.")]
    private void RunActiveJobs(List<Run> runs, CancellationToken stoppingToken)
    {
        var scope = serviceProvider.CreateScope();
        var tasks = new List<Task>(runs.Count);

        foreach (var run in runs)
        {
            var job = (IJob)scope.ServiceProvider.GetRequiredService(run.Type);

            // We don't want to await jobs explicitly because that
            // could interfere with other job runs
            var jobRun = job.Run(run.Context, stoppingToken);
            tasks.Add(jobRun);
        }

        _ = Task.WhenAll(tasks).ContinueWith(_ => scope.Dispose(), TaskScheduler.Default);
    }

    private List<Run> GetNextJobRuns()
    {
        var entries = registry.GetAllInstantJobsAndClear()
            .Select(instant => new Run(instant.Type, instant.Context))
            .ToList();

        var utcNow = timeProvider.GetUtcNow().DateTime;
        foreach (var cron in registry.GetAllCronJobs())
        {
            var runDates = cron.CrontabSchedule.GetNextOccurrences(utcNow, utcNow.Add(options.TimerInterval)).ToArray();
            if (runDates.Length > 0)
            {
                entries.Add(new Run(cron.Type, new JobExecutionContext(cron.Context.Parameter)));
            }
        }

        return entries;
    }

    private record struct Run(Type Type, JobExecutionContext Context);
}
