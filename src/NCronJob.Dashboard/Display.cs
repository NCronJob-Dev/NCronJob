using System.Globalization;

namespace NCronJob.Dashboard;

internal static class Display
{
    public static string Relative(DateTimeOffset at, DateTimeOffset now)
    {
        var offset = at - now;
        var text = Compact(offset.Duration());
        return offset >= TimeSpan.Zero ? $"in {text}" : $"{text} ago";
    }

    public static string Duration(TimeSpan duration) => duration switch
    {
        { TotalSeconds: < 1 } => string.Create(CultureInfo.InvariantCulture, $"{duration.TotalMilliseconds:0} ms"),
        { TotalMinutes: < 1 } => string.Create(CultureInfo.InvariantCulture, $"{duration.TotalSeconds:0.0} s"),
        _ => Compact(duration),
    };

    public static string Timestamp(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    public static string Time(DateTimeOffset at) =>
        at.UtcDateTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string Compact(TimeSpan span) => span switch
    {
        { TotalSeconds: < 60 } => string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalSeconds}s"),
        { TotalMinutes: < 60 } => string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalMinutes}m {span.Seconds}s"),
        { TotalHours: < 24 } => string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}h {span.Minutes}m"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalDays}d {span.Hours}h"),
    };
}
