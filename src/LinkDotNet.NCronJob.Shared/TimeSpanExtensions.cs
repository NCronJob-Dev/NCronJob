using System.Text;

namespace LinkDotNet.NCronJob.Shared;

internal static class TimeSpanExtensions
{
    public static string ToHumanDuration(this TimeSpan duration, bool displaySign = true)
    {
        var builder = new StringBuilder();
        if (displaySign)
        {
            builder.Append(duration.TotalMilliseconds < 0 ? "-" : "+");
        }

        duration = duration.Duration();

        if (duration.Days > 0)
        {
            builder.Append($"{duration.Days}d ");
        }

        if (duration.Hours > 0)
        {
            builder.Append($"{duration.Hours}h ");
        }

        if (duration.Minutes > 0)
        {
            builder.Append($"{duration.Minutes}m ");
        }

        if (duration is { TotalHours: < 1, Seconds: > 0 })
        {
            builder.Append(duration.Seconds);
            builder.Append("s ");
        }

        if (builder.Length <= 1)
        {
            builder.Append(" <1ms ");
        }

        builder.Remove(builder.Length - 1, 1);

        return builder.ToString();
    }

}
