using Cronos;

namespace NCronJob;

/// <summary>
/// Immutable cron schedule of a job: the expression as the user wrote it, its parsed form and the time zone.
/// </summary>
/// <param name="UserDefinedCronExpression">
/// The unhandled cron expression from the user. <see cref="CronExpression.ToString"/> alters the expression
/// (whitespace is removed, steps are expanded), so comparisons by string representation must use this value.
/// </param>
/// <param name="CronExpression">The parsed expression, or the never-firing expression while the schedule is disabled.</param>
/// <param name="TimeZone">The time zone the expression is evaluated in, UTC when <c>null</c>.</param>
internal sealed record JobSchedule(
    string? UserDefinedCronExpression,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone)
{
    private static readonly CronExpression NotReachableCronDefinition = CronExpression.Parse("* * 31 2 *");

    public static readonly JobSchedule None = new(null, null, null);

    public bool IsEnabled => CronExpression is null || CronExpression != NotReachableCronDefinition;

    public TimeZoneInfo EffectiveTimeZone => TimeZone ?? TimeZoneInfo.Utc;

    public static JobSchedule Parse(string userDefinedCronExpression, TimeZoneInfo? timeZone) =>
        new(userDefinedCronExpression, ParseCronExpression(userDefinedCronExpression.Trim()), timeZone);

    public JobSchedule Disable() => this with { CronExpression = NotReachableCronDefinition };

    public JobSchedule Enable() => this with
    {
        CronExpression = UserDefinedCronExpression is not null
            ? ParseCronExpression(UserDefinedCronExpression.Trim())
            : null
    };

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset utcNow) =>
        CronExpression?.GetNextOccurrence(utcNow, EffectiveTimeZone);

    private static CronExpression ParseCronExpression(string expression)
    {
        if (expression.StartsWith('@'))
        {
            return CronExpression.TryParse(expression, CronFormat.IncludeSeconds, out var macroExpression)
                ? macroExpression
                : throw new ArgumentException($"Unknown cron macro '{expression}'.", nameof(expression));
        }

        var precisionRequired = DetermineAndValidatePrecision(expression);

        var cf = precisionRequired ? CronFormat.IncludeSeconds : CronFormat.Standard;

        return CronExpression.TryParse(expression, cf, out var cronExpression)
            ? cronExpression
            : throw new InvalidOperationException("Invalid cron expression");
    }

    private static bool DetermineAndValidatePrecision(string cronExpression)
    {
        var parts = cronExpression.Split(' ');
        var precisionRequired = parts.Length == 6;

        var expectedLength = precisionRequired ? 6 : 5;
        if (parts.Length != expectedLength)
        {
            var precisionText = precisionRequired ? "second precision" : "minute precision";
            throw new ArgumentException($"Invalid cron expression format for {precisionText}.", nameof(cronExpression));
        }

        return precisionRequired;
    }
}
