
namespace NCronJob;

/// <inheritdoc />
internal sealed record JobExecutionContext : IJobExecutionContext
{
    internal bool ExecuteChildren = true;

    public JobExecutionContext(JobRun jobRun)
    {
        JobRun = jobRun;
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public object? Output { get; set; }

    /// <inheritdoc />
    public int Attempts { get; internal set; }

    /// <inheritdoc />
    public string? JobName => JobRun.JobDefinition.CustomName;

    Type? IJobExecutionContext.JobType
        => JobRun.JobDefinition.Type;

    public TriggerType TriggerType => JobRun.TriggerType;

    /// <summary>The Job Run instance.</summary>
    internal JobRun JobRun { get; }

    /// <inheritdoc />
    public object? Parameter => JobRun.Parameter;

    /// <inheritdoc />
    public Guid CorrelationId => JobRun.CorrelationId;

    /// <inheritdoc />
    public object? ParentOutput => JobRun.ParentOutput;

    /// <inheritdoc />
    public void SkipChildren() => ExecuteChildren = false;
}
