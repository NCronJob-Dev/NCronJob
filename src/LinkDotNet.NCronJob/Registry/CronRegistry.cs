using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

internal sealed partial class CronRegistry : IInstantJobRegistry
{
    private readonly JobExecutor jobExecutor;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CronRegistry> logger;
    private readonly ImmutableArray<RegistryEntry> cronJobs;

    public CronRegistry(
        IEnumerable<RegistryEntry> jobs,
        JobExecutor jobExecutor,
        TimeProvider timeProvider,
        ILogger<CronRegistry> logger)
    {
        this.jobExecutor = jobExecutor;
        this.timeProvider = timeProvider;
        this.logger = logger;
        cronJobs = [..jobs.Where(c => c.CronExpression is not null)];
    }

    public IReadOnlyCollection<RegistryEntry> GetAllCronJobs() => cronJobs;

    /// <inheritdoc />
    public void RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob => RunScheduledJob<TJob>(TimeSpan.Zero, parameter, token);

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(TimeSpan delay, object? parameter = null, CancellationToken token = default)
    {
        token.Register(() => LogCancellationRequested(parameter));

        var run = new RegistryEntry(typeof(TJob), parameter, null, null);

        _ = Task.Run<Task>(async () =>
        {
            var jobName = typeof(TJob).Name;

            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await TaskExtensions.LongDelaySafe(delay, timeProvider, token);
                }

                using (logger.BeginScope(new Dictionary<string, object>
                       {
                           { "JobName", jobName },
                           { "JobTypeFullName", typeof(TJob).FullName ?? jobName }
                       }))
                {
                    await jobExecutor.RunJob(run, CancellationToken.None);
                }
            }
            catch
            {
                LogCancellationNotice(jobName);
            }
        }, token);
    }

    /// <inheritdoc />
    public void RunScheduledJob<TJob>(DateTimeOffset startDate, object? parameter = null, CancellationToken token = default) where TJob : IJob
    {
        var utcNow = timeProvider.GetUtcNow();
        ArgumentOutOfRangeException.ThrowIfLessThan(startDate, utcNow);

        var delay = startDate - utcNow;
        RunScheduledJob<TJob>(delay, parameter, token);
    }

    [LoggerMessage(LogLevel.Warning, "Job {JobName} cancelled by request.")]
    private partial void LogCancellationNotice(string jobName);

    [LoggerMessage(LogLevel.Debug, "Cancellation requested for CronRegistry {Parameter}.")]
    private partial void LogCancellationRequested(object? parameter);
}

