using System.Collections.Frozen;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

internal sealed partial class CronRegistry : IInstantJobRegistry
{
    private readonly JobExecutor jobExecutor;
    private readonly ILogger<CronRegistry> logger;
    private readonly FrozenSet<RegistryEntry> cronJobs;

    public CronRegistry(IEnumerable<RegistryEntry> jobs,
        JobExecutor jobExecutor,
        ILogger<CronRegistry> logger)
    {
        this.jobExecutor = jobExecutor;
        this.logger = logger;
        cronJobs = jobs.Where(c => c.CrontabSchedule is not null).ToFrozenSet();
    }

    public IReadOnlyCollection<RegistryEntry> GetAllCronJobs() => cronJobs;

    /// <inheritdoc />
    public Task RunInstantJob<TJob>(object? parameter = null, CancellationToken token = default)
        where TJob : IJob
    {
        token.Register(() => Console.WriteLine("Cancellation requested for CronRegistry from token."));

        var executionContext = new JobExecutionContext(parameter);
        var run = new RegistryEntry(typeof(TJob), executionContext, null);

        var jobName = typeof(TJob).Name;
        _ = Task.Run(async () =>
        {
            try
            {
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

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Warning, "Job {JobName} cancelled by request.")]
    private partial void LogCancellationNotice(string jobName);
}

