using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// Validates cron expressions and previews their occurrences using the same rules NCronJob applies when scheduling jobs.
/// </summary>
public static class CronSchedule
{
    /// <summary>
    /// Checks whether <paramref name="cronExpression"/> would be accepted by <c>WithCronExpression</c>.
    /// </summary>
    /// <param name="cronExpression">A 5-field (minute precision), 6-field (second precision) or macro (e.g. <c>@daily</c>) expression.</param>
    /// <param name="error">The reason the expression was rejected, or <c>null</c> when it is valid.</param>
    /// <returns><c>true</c> when the expression is valid; otherwise <c>false</c>.</returns>
    public static bool IsValid(string cronExpression, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        try
        {
            JobSchedule.Parse(cronExpression, timeZone: null);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> upcoming occurrences of <paramref name="cronExpression"/> after <paramref name="from"/>.
    /// </summary>
    /// <param name="cronExpression">A 5-field (minute precision), 6-field (second precision) or macro (e.g. <c>@daily</c>) expression.</param>
    /// <param name="count">The maximum number of occurrences to return. Must be positive.</param>
    /// <param name="from">The exclusive start of the search.</param>
    /// <param name="timeZone">The time zone the expression is evaluated in. Defaults to UTC.</param>
    /// <returns>The occurrences in UTC. Fewer than <paramref name="count"/> when the schedule runs out, empty when it is unreachable (e.g. February 30th).</returns>
    /// <remarks>
    /// To preview a registered job, read its schedule with <see cref="IRuntimeJobRegistry.TryGetSchedule"/> and pass it here.
    /// </remarks>
    /// <exception cref="ArgumentException">The expression is invalid.</exception>
    /// <exception cref="InvalidOperationException">The expression is invalid.</exception>
    public static IReadOnlyList<DateTimeOffset> GetNextOccurrences(
        string cronExpression,
        int count,
        DateTimeOffset from,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var schedule = JobSchedule.Parse(cronExpression, timeZone);
        var occurrences = new List<DateTimeOffset>(count);

        for (var next = schedule.GetNextOccurrence(from);
             next is not null && occurrences.Count < count;
             next = schedule.GetNextOccurrence(next.Value))
        {
            occurrences.Add(next.Value.ToUniversalTime());
        }

        return occurrences;
    }
}
