using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a background service that schedules jobs based on a cron expression.
/// </summary>
internal sealed partial class CronScheduler : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly CronRegistry registry;
    private readonly TimeProvider timeProvider;
    private readonly NCronJobOptions options;
    private readonly ILogger<CronScheduler> logger;

    public CronScheduler(
        IServiceProvider serviceProvider,
        CronRegistry registry,
        TimeProvider timeProvider,
        NCronJobOptions options,
        ILogger<CronScheduler> logger)
    {
        this.serviceProvider = serviceProvider;
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
            // We don't want to await jobs explicitly because that
            // could interfere with other job runs
            RunActiveJobs(runs, stoppingToken);
            runs = GetNextJobRuns();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tasks are not awaited.")]
    private void RunActiveJobs(List<RegistryEntry> runs, CancellationToken stoppingToken)
    {
        foreach (var run in runs)
        {
            var scope = serviceProvider.CreateScope();
            if (scope.ServiceProvider.GetService(run.Type) is not IJob job)
            {
                LogJobNotRegistered(run.Type);
                continue;
            }

            RunJob(run, job, scope, stoppingToken);
        }
    }

    private void RunJob(RegistryEntry run, IJob job, IServiceScope serviceScope, CancellationToken stoppingToken)
    {
        try
        {
            LogRunningJob(run.Type, run.IsolationLevel);
            GetJobTask()
                .ContinueWith(
                    task => AfterJobCompletionTask(task.Exception),
                    TaskScheduler.Current)
                .ConfigureAwait(false);
        }
        catch (Exception exc) when (exc is not OperationCanceledException or AggregateException)
        {
            // This part is only reached if the synchronous part of the job throws an exception
            AfterJobCompletionTask(exc);
        }

        Task GetJobTask()
        {
            return run.IsolationLevel switch
            {
                IsolationLevel.None => job.RunAsync(run.Context, stoppingToken),
                IsolationLevel.NewTask => Task.Run(() => job.RunAsync(run.Context, stoppingToken), stoppingToken),
                _ => throw new InvalidOperationException($"Unknown isolation level {run.IsolationLevel}"),
            };
        }

        void AfterJobCompletionTask(Exception? exc)
        {
            var notificationServiceType = typeof(IJobNotificationHandler<>).MakeGenericType(run.Type);

            if (serviceScope.ServiceProvider.GetService(notificationServiceType) is IJobNotificationHandler notificationService)
            {
                try
                {
                    notificationService.HandleAsync(run.Context, exc, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception innerExc) when (innerExc is not OperationCanceledException or AggregateException)
                {
                    // We don't want to throw exceptions from the notification service
                }
            }

            serviceScope.Dispose();
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
