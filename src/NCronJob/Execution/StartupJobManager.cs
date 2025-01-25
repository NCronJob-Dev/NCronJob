using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace NCronJob;

internal partial class StartupJobManager(
    JobRegistry jobRegistry,
    JobProcessor jobProcessor,
    JobExecutionProgressObserver observer,
    TimeProvider timeProvider,
    ILogger<StartupJobManager> logger)
{
    public async Task ProcessStartupJobs(CancellationToken stopToken)
    {
        var startupJobs = jobRegistry.GetAllOneTimeJobs();

        if (startupJobs.Count == 0)
        {
            return;
        }

        LogStartupJobsStart(logger, timeProvider.GetUtcNow());

        List<JobRun> jobRuns = [];
        var startupTasks = startupJobs.Select(definition =>
        {
            var jobRun = JobRun.Create(timeProvider, observer.Report, definition);

            jobRuns.Add(jobRun);
            return CreateExecutionTask(jobRun, stopToken);
        });

        await Task.WhenAll(startupTasks).ConfigureAwait(false);

        LogStartupJobsCompletion(logger, timeProvider.GetUtcNow());

        Exception[] faults = jobRuns
            .Where(jr => jr.JobDefinition.ShouldCrashOnStartupFailure == true && jr.CurrentState.Type == JobStateType.Faulted)
            .Select(jr => jr.CurrentState.Fault)
            .Cast<Exception>()
            .ToArray();

        if (faults.Length == 0)
        {
            return;
        }

        StringBuilder sb = new();
        sb.AppendLine("At least one of the startup jobs failed");
        foreach (var fault in faults)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {fault}");
        }

        throw new InvalidOperationException(sb.ToString());
    }

    private async Task CreateExecutionTask(JobRun job, CancellationToken stopToken) =>
        await jobProcessor.ProcessJobAsync(job, stopToken).ConfigureAwait(false);

    [LoggerMessage(LogLevel.Information, "Triggering startup jobs execution at {at:o}")]
    private static partial void LogStartupJobsStart(ILogger logger, DateTimeOffset at);

    [LoggerMessage(LogLevel.Information, "Completed startup jobs execution at {at:o}")]
    private static partial void LogStartupJobsCompletion(ILogger logger, DateTimeOffset at);
}
