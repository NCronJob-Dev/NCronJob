using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NCronJob;

internal static class NCronJobMetrics
{
    private static readonly Meter Meter = new(
        NCronJobDiagnostics.MeterName,
        typeof(NCronJobMetrics).Assembly.GetName().Version?.ToString());

    private static readonly Counter<long> Runs = Meter.CreateCounter<long>(
        "ncronjob.job.runs", "{run}", "Number of job runs that started executing.");

    private static readonly Counter<long> Failures = Meter.CreateCounter<long>(
        "ncronjob.job.failures", "{run}", "Number of job runs that ended as faulted.");

    private static readonly Counter<long> Retries = Meter.CreateCounter<long>(
        "ncronjob.job.retries", "{retry}", "Number of retry attempts of job runs.");

    private static readonly Counter<long> Skipped = Meter.CreateCounter<long>(
        "ncronjob.job.skipped", "{run}", "Number of job runs that were skipped.");

    private static readonly Counter<long> Expired = Meter.CreateCounter<long>(
        "ncronjob.job.expired", "{run}", "Number of job runs that expired before they could start.");

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "ncronjob.job.duration", "s", "Duration of job runs, including retries.");

    public static void RecordStateChange(string jobName, TriggerType triggerType, JobStateType state)
    {
        var counter = state switch
        {
            JobStateType.Running => Runs,
            JobStateType.Faulted => Failures,
            JobStateType.Retrying => Retries,
            JobStateType.Skipped => Skipped,
            JobStateType.Expired => Expired,
            _ => null,
        };

        if (counter is { Enabled: true })
        {
            counter.Add(1, TagsFor(jobName, triggerType));
        }
    }

    public static void RecordDuration(string jobName, TriggerType triggerType, long startTimestamp)
    {
        if (Duration.Enabled)
        {
            Duration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, TagsFor(jobName, triggerType));
        }
    }

    private static TagList TagsFor(string jobName, TriggerType triggerType) => new()
    {
        { "ncronjob.job.name", jobName },
        { "ncronjob.trigger_type", triggerType.ToString() },
    };
}
