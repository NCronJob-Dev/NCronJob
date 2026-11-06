using System.Globalization;
using System.Text;

namespace NCronJob;

internal static class StartupSummaryFormatter
{
    private const string MinutePrecisionFormat = "yyyy-MM-dd HH:mm";
    private const string SecondPrecisionFormat = "yyyy-MM-dd HH:mm:ss";

    public static string Format(IReadOnlyCollection<(JobDefinition Job, JobRun? FirstRun)> scheduledJobs)
    {
        var rows = scheduledJobs.Select(ToRow).ToList();

        var summary = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"NCronJob started with {rows.Count} {(rows.Count == 1 ? "job" : "jobs")}");

        if (rows.Count == 0)
        {
            return summary.ToString();
        }

        summary.Append(':');

        var nameWidth = rows.Max(r => r.Name.Length);
        var cronWidth = rows.Max(r => r.Cron.Length);
        var timeZoneWidth = rows.Max(r => r.TimeZone.Length);

        foreach (var row in rows)
        {
            summary
                .AppendLine()
                .Append("  ")
                .Append(row.Name.PadRight(nameWidth))
                .Append("   ")
                .Append(row.Cron.PadRight(cronWidth))
                .Append(' ')
                .Append(row.TimeZone.PadRight(timeZoneWidth))
                .Append(' ')
                .Append(row.Next);
        }

        return summary.ToString();
    }

    private static (string Name, string Cron, string TimeZone, string Next) ToRow((JobDefinition Job, JobRun? FirstRun) entry)
    {
        var (cron, timeZone) = entry.Job.GetSchedule();
        timeZone ??= TimeZoneInfo.Utc;

        return (entry.Job.Name, cron ?? string.Empty, timeZone.Id, DescribeNextRun(entry, timeZone));
    }

    private static string DescribeNextRun((JobDefinition Job, JobRun? FirstRun) entry, TimeZoneInfo timeZone)
    {
        if (entry.FirstRun is { } run)
        {
            var nextRun = TimeZoneInfo.ConvertTime(run.RunAt, timeZone);
            var format = nextRun.Second == 0 ? MinutePrecisionFormat : SecondPrecisionFormat;
            return "next " + nextRun.ToString(format, CultureInfo.InvariantCulture);
        }

        return entry.Job.IsEnabled ? "no upcoming run" : "disabled";
    }
}
