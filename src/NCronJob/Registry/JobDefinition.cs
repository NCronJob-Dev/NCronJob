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
    public bool IsStartupJob { get; set; }

    public string JobName { get; } = JobName ?? Type.Name;

    public CronExpression? CronExpression { get; set; } = CronExpression;

    public object? Parameter { get; set; } = Parameter;

    public TimeZoneInfo? TimeZone { get; set; } = TimeZone;

    public List<JobDefinition> RunWhenSuccess { get; set; } = [];

    public List<JobDefinition> RunWhenFaulted { get; set; } = [];

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    public string JobFullName => JobName == Type.Name
        ? Type.FullName ?? JobName
        : $"{typeof(DynamicJobFactory).Namespace}.{JobName}";

    internal JobExecutionAttributes JobPolicyMetadata { get; } = JobPolicyMetadata ?? new JobExecutionAttributes(Type);
    public RetryPolicyAttribute? RetryPolicy => JobPolicyMetadata.RetryPolicy;
    public SupportsConcurrencyAttribute? ConcurrencyPolicy => JobPolicyMetadata.ConcurrencyPolicy;

    public JobDefinition CreateInstantVersion() => new(Type, null, null, TimeZone, Type.Name, JobPolicyMetadata);
}
