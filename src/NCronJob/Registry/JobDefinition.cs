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

    private JobSchedule schedule = JobSchedule.None;

    public CronExpression? CronExpression => schedule.CronExpression;

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
    public string? UserDefinedCronExpression => schedule.UserDefinedCronExpression;

    public object? Parameter { get; private set; }

    public TimeZoneInfo? TimeZone => schedule.TimeZone;

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    public string JobFullName { get; }

    private JobExecutionAttributes JobPolicyMetadata { get; }
    public RetryPolicyBaseAttribute? RetryPolicy => JobPolicyMetadata.RetryPolicy;
    public SupportsConcurrencyAttribute? ConcurrencyPolicy => JobPolicyMetadata.ConcurrencyPolicy;
    public TimeSpan Timeout { get; private set; } = System.Threading.Timeout.InfiniteTimeSpan;
    public TimeSpan? JobRunExpiry { get; private set; }

    /// <summary>
    /// Conditional predicate that must return true for the job to execute.
    /// If false, the job will be skipped without instantiation.
    /// Evaluated once before job instantiation and not re-evaluated during retries.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, ValueTask<bool>>? Condition { get; private set; }

    [MemberNotNullWhen(true, nameof(Type))]
    [MemberNotNullWhen(false, nameof(Delegate))]
    public bool IsTypedJob { get; }

    public bool IsEnabled => schedule.IsEnabled;

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
        return type.FullName is null // FullName is later required to properly identify the JobDefinition
            || !type.GetInterfaces().Contains(typeof(IJob))
            ? throw new InvalidOperationException($"Type '{type}' doesn't implement '{nameof(IJob)}'.")
            : new(name, type, parameter);
    }

    public static JobDefinition CreateUntyped(
        string? name,
        Delegate jobDelegate)
    => new(name, jobDelegate);

    public void Disable()
    {
        UpdateSchedule(current => current with { CronExpression = NotReachableCronDefinition });
    }

    public void Enable()
    {
        UpdateSchedule(current => current with
        {
            CronExpression = current.UserDefinedCronExpression is not null
                ? GetCronExpression(current.UserDefinedCronExpression.Trim())
                : null
        });
    }

    // Compare-and-swap so a concurrent schedule change is never overwritten by a stale snapshot.
    private void UpdateSchedule(Func<JobSchedule, JobSchedule> update)
    {
        JobSchedule current;
        do
        {
            current = schedule;
        }
        while (Interlocked.CompareExchange(ref schedule, update(current), current) != current);
    }

    public DateTimeOffset? GetNextCronOccurrence(DateTimeOffset utcNow)
    {
        var current = schedule;
        return current.CronExpression?.GetNextOccurrence(utcNow, current.TimeZone ?? TimeZoneInfo.Utc);
    }

    public (string? UserDefinedCronExpression, TimeZoneInfo? TimeZone) GetSchedule()
    {
        var current = schedule;
        return (current.UserDefinedCronExpression, current.UserDefinedCronExpression is null ? null : current.TimeZone ?? TimeZoneInfo.Utc);
    }

    public RecurringJobSchedule ToRecurringJobSchedule()
    {
        var current = schedule;
        return new RecurringJobSchedule(
            JobName: CustomName,
            Type: Type,
            IsTypedJob: IsTypedJob,
            CronExpression: current.UserDefinedCronExpression!,
            IsEnabled: current.IsEnabled,
            TimeZone: current.TimeZone ?? TimeZoneInfo.Utc);
    }

    public void UpdateWith(JobOption? jobOption)
    {
        if (jobOption is null)
        {
            return;
        }

        if (jobOption.CronExpression is not null)
        {
            schedule = new JobSchedule(
                jobOption.CronExpression,
                GetCronExpression(jobOption.CronExpression.Trim()),
                jobOption.TimeZoneInfo);
        }

        if (jobOption.HasParameter)
        {
            Parameter = jobOption.Parameter;
        }

        if (jobOption.ShouldCrashOnStartupFailure is not null)
        {
            ShouldCrashOnStartupFailure = jobOption.ShouldCrashOnStartupFailure;
        }

        if (jobOption.Timeout is not null)
        {
            Timeout = jobOption.Timeout.Value;
        }

        if (jobOption.JobRunExpiry is not null)
        {
            JobRunExpiry = jobOption.JobRunExpiry.Value;
        }

        if (jobOption.Conditions is { Count: > 0 })
        {
            var previousCondition = Condition;
            var addedConditions = jobOption.Conditions.ToArray();

            Condition = async (sp, ct) =>
            {
                if (previousCondition is not null && !await previousCondition(sp, ct).ConfigureAwait(false))
                    return false;

                foreach (var condition in addedConditions)
                {
                    if (!await condition(sp, ct).ConfigureAwait(false))
                        return false;
                }

                return true;
            };
        }
    }

    public void MarkAsStartupJob(bool shouldCrashOnFailure)
    {
        if (IsStartupJob)
        {
            throw new InvalidOperationException($"Job '{Name}' is already defined as a startup job.");
        }

        UpdateWith(new JobOption { ShouldCrashOnStartupFailure = shouldCrashOnFailure });
    }

    public IJob? ResolveJob(IServiceProvider scopedServiceProvider)
    {
        return IsTypedJob ? (IJob?)scopedServiceProvider.GetService(Type) : new DynamicJobFactory(scopedServiceProvider, Delegate);
    }

    public bool IsExemptFromUniqueParameterizedTypedJobCheck =>
        CustomName is not null
        || CronExpression is not null
        || IsStartupJob
        || !IsTypedJob
        || Parameter is null;

    private Delegate? Delegate { get; }

    private static CronExpression GetCronExpression(string expression)
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

    private static readonly CronExpression NotReachableCronDefinition = CronExpression.Parse("* * 31 2 *");

    private sealed record JobSchedule(
        string? UserDefinedCronExpression,
        CronExpression? CronExpression,
        TimeZoneInfo? TimeZone)
    {
        public static readonly JobSchedule None = new(null, null, null);

        public bool IsEnabled => CronExpression is null || CronExpression != NotReachableCronDefinition;
    }
}
