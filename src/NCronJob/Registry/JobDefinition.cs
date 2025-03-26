using Cronos;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal sealed record JobDefinition
{
    private JobDefinition(
        string? customName,
        Type type,
        object? parameter)
    {
        CustomName = customName;

        Type = type;
        IsTypedJob = true;
        JobFullName = type.FullName!;

        Parameter = parameter;
        JobPolicyMetadata = new JobExecutionAttributes(type);
    }

    private JobDefinition(
        string? customName,
        Delegate jobDelegate)
    {
        CustomName = customName;

        Delegate = jobDelegate;
        IsTypedJob = false;
        JobFullName = customName is not null ?
            $"Untyped job {customName}" :
            $"Untyped job {typeof(DynamicJobFactory).Namespace}.{DynamicJobNameGenerator.GenerateJobName(jobDelegate)}";

        JobPolicyMetadata = new JobExecutionAttributes(jobDelegate);
    }

    public string Name => CustomName is not null ? $"{CustomName} ({JobFullName})" : JobFullName;

    public Type? Type { get; }

    public bool IsStartupJob => ShouldCrashOnStartupFailure is not null;

    public bool? ShouldCrashOnStartupFailure { get; private set; }

    public string? CustomName { get; }

    public CronExpression? CronExpression { get; private set; }

    /// <summary>
    /// This is the unhandled cron expression from the user. Using <see cref="CronExpression.ToString"/> will alter the expression.
    /// For example:
    /// <code>
    /// var cron = CronExpression.Parse("  0 0 1 * *"); // Extra whitespace
    /// cron.ToString(); // No extra whitespace: 0 0 1 * *
    /// var cron = CronExpression.Parse("*/2 * * *");
    /// cron.ToString(); // 0,2,4,6,8,10,12,... * * * *
    /// </code>
    /// If the user wants to compare the schedule by its string representation, this property should be used.
    /// </summary>
    public string? UserDefinedCronExpression { get; private set; }

    public object? Parameter { get; private set; }

    public TimeZoneInfo? TimeZone { get; private set; }

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    public string JobFullName { get; }

    private JobExecutionAttributes JobPolicyMetadata { get; }
    public RetryPolicyBaseAttribute? RetryPolicy => JobPolicyMetadata.RetryPolicy;
    public SupportsConcurrencyAttribute? ConcurrencyPolicy => JobPolicyMetadata.ConcurrencyPolicy;

    [MemberNotNullWhen(true, nameof(Type))]
    [MemberNotNullWhen(false, nameof(Delegate))]
    public bool IsTypedJob { get; }

    public bool IsEnabled => CronExpression is null
        || CronExpression != NotReacheableCronDefinition;

    public static JobDefinition CreateTyped(
        Type type,
        object? parameter)
    {
        return CreateTyped(null, type, parameter);
    }

    public static JobDefinition CreateTyped(
        string? name,
        Type type,
        object? parameter)
    {
        if (type.FullName is null // FullName is later required to properly identify the JobDefinition
            || !type.GetInterfaces().Contains(typeof(IJob)))
        {
            throw new InvalidOperationException($"Type '{type}' doesn't implement '{nameof(IJob)}'.");
        }

        return new(name, type, parameter);
    }

    public static JobDefinition CreateUntyped(
        string? name,
        Delegate jobDelegate)
    => new(name, jobDelegate);

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

    public DateTimeOffset? GetNextCronOccurrence(DateTimeOffset utcNow)
        => CronExpression?.GetNextOccurrence(utcNow, TimeZone ?? TimeZoneInfo.Utc);

    public (string? UserDefinedCronExpression, TimeZoneInfo? TimeZone) GetSchedule()
        => (UserDefinedCronExpression, UserDefinedCronExpression is null ? null : TimeZone ?? TimeZoneInfo.Utc);

    public RecurringJobSchedule ToRecurringJobSchedule()
    {
        return new RecurringJobSchedule(
            JobName: CustomName,
            Type: Type,
            IsTypedJob: IsTypedJob,
            CronExpression: UserDefinedCronExpression!,
            IsEnabled: IsEnabled,
            TimeZone: TimeZone ?? TimeZoneInfo.Utc);
    }

    public void UpdateWith(JobOption? jobOption)
    {
        if (jobOption is null)
        {
            return;
        }

        if (jobOption.CronExpression is not null)
        {
            UserDefinedCronExpression = jobOption.CronExpression;
            CronExpression = GetCronExpression(jobOption.CronExpression.Trim());

            TimeZone = jobOption.TimeZoneInfo;
        }

        if (jobOption.Parameter is not null)
        {
            Parameter = jobOption.Parameter;
        }

        if (jobOption.ShouldCrashOnStartupFailure is not null)
        {
            ShouldCrashOnStartupFailure = jobOption.ShouldCrashOnStartupFailure;
        }
    }

    public IJob? ResolveJob(IServiceProvider scopedServiceProvider)
    {
        if (IsTypedJob)
        {
            return (IJob?)scopedServiceProvider.GetService(Type);
        }

        return new DynamicJobFactory(scopedServiceProvider, Delegate);
    }

    private Delegate? Delegate { get; }

    private static CronExpression GetCronExpression(string expression)
    {
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

    private static readonly CronExpression NotReacheableCronDefinition = CronExpression.Parse("* * 31 2 *");
}
