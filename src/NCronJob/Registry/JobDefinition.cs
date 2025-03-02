using Cronos;

namespace NCronJob;

internal sealed record JobDefinition(
    Type Type,
    object? Parameter,
    CronExpression? CronExpression,
    TimeZoneInfo? TimeZone,
    string? JobName = null,
    JobExecutionAttributes? JobPolicyMetadata = null)
{
    public bool IsStartupJob => ShouldCrashOnStartupFailure is not null;

    public bool? ShouldCrashOnStartupFailure { get; set; }

    public string JobName { get; } = JobName ?? Type.Name;

    public string? CustomName { get; set; }

    public CronExpression? CronExpression { get; set; } = CronExpression;

    /// <summary>
    /// This is the unhandled cron expression from the user. Using <see cref="CronExpression.ToString"/> will alter the expression.
    /// For example:
    /// <code>
    /// var cron = CronExpression.Parse("0  0 1 * *"); // Extra whitespace
    /// cron.ToString(); // No extra whitespace: 0 0 1 * *
    /// var cron = CronExpression.Parse("*/2 * * *");
    /// cron.ToString(); // 0,2,4,6,8,10,12,... * * * *
    /// </code>
    /// If the user wants to compare the schedule by its string representation, this property should be used.
    /// </summary>
    public string? UserDefinedCronExpression { get; set; }

    public object? Parameter { get; set; } = Parameter;

    public TimeZoneInfo? TimeZone { get; set; } = TimeZone;

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    public string JobFullName => JobName == Type.Name
        ? Type.FullName ?? JobName
        : $"{typeof(DynamicJobFactory).Namespace}.{JobName}";

    private JobExecutionAttributes JobPolicyMetadata { get; } = JobPolicyMetadata ?? new JobExecutionAttributes(Type);
    public RetryPolicyBaseAttribute? RetryPolicy => JobPolicyMetadata.RetryPolicy;
    public SupportsConcurrencyAttribute? ConcurrencyPolicy => JobPolicyMetadata.ConcurrencyPolicy;

    public bool IsAnonymousJob => Type == typeof(DynamicJobFactory);

    private bool IsDisabled => CronExpression == NotReacheableCronDefinition;

    public bool IsEnabled => CronExpression is null || !IsDisabled;

    public void Disable()
    {
        CronExpression = NotReacheableCronDefinition;
    }

    public void Enable()
    {
        if (UserDefinedCronExpression is not null)
        {
            CronExpression = CronExpression.Parse(UserDefinedCronExpression);
            return;
        }

        CronExpression = null;
    }

    public DateTimeOffset? GetNextCronOccurrence(DateTimeOffset utcNow, TimeZoneInfo? timeZone)
        => CronExpression?.GetNextOccurrence(utcNow, timeZone);

    private static readonly CronExpression NotReacheableCronDefinition = CronExpression.Parse("* * 31 2 *");
}
