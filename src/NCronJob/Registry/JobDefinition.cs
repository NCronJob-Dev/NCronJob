using Cronos;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal sealed record JobDefinition
{
    private JobDefinition(
        Type type,
        object? parameter)
    {
        Type = type;
        IsTypedJob = true;
        JobFullName = type.FullName!;
        JobName = type.Name;

        Parameter = parameter;
        JobPolicyMetadata = new JobExecutionAttributes(type);
    }

    private JobDefinition(
        string dynamicHash,
        JobExecutionAttributes jobPolicyMetadata)
    {
        Type = typeof(DynamicJobFactory);
        IsTypedJob = false;
        JobFullName = $"{typeof(DynamicJobFactory).Namespace}.{dynamicHash}";
        JobName = dynamicHash;

        JobPolicyMetadata = jobPolicyMetadata;
    }

    public Type Type { get; }

    public bool IsStartupJob => ShouldCrashOnStartupFailure is not null;

    public bool? ShouldCrashOnStartupFailure { get; set; }

    public string JobName { get; }

    public string? CustomName { get; set; }

    public CronExpression? CronExpression { get; set; }

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

    public object? Parameter { get; set; }

    public TimeZoneInfo? TimeZone { get; set; }

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    public string JobFullName { get; }

    private JobExecutionAttributes JobPolicyMetadata { get; }
    public RetryPolicyBaseAttribute? RetryPolicy => JobPolicyMetadata.RetryPolicy;
    public SupportsConcurrencyAttribute? ConcurrencyPolicy => JobPolicyMetadata.ConcurrencyPolicy;

    public Type? ExposedType => IsTypedJob ? Type : null;

    [MemberNotNullWhen(true, nameof(ExposedType))]
    public bool IsTypedJob { get; }

    private bool IsDisabled => CronExpression == NotReacheableCronDefinition;

    public bool IsEnabled => CronExpression is null || !IsDisabled;

    public static JobDefinition CreateTyped(
        Type type,
        object? parameter)
    {
        if (type.FullName is null // FullName is later required to properly identify the JobDefinition
            || !type.GetInterfaces().Contains(typeof(IJob)))
        {
            throw new InvalidOperationException($"Type '{type}' doesn't implement '{nameof(IJob)}'.");
        }

        return new(type, parameter);
    }

    public static JobDefinition CreateUntyped(
        string dynamicHash,
        JobExecutionAttributes jobPolicyMetadata)
    => new(dynamicHash, jobPolicyMetadata);

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

    public RecurringJobSchedule ToRecurringJobSchedule()
    {
        return new RecurringJobSchedule(
            JobName: CustomName,
            Type: ExposedType,
            IsTypedJob: IsTypedJob,
            CronExpression: UserDefinedCronExpression!,
            IsEnabled: IsEnabled,
            TimeZone: TimeZone!);
    }

    private static readonly CronExpression NotReacheableCronDefinition = CronExpression.Parse("* * 31 2 *");
}
