
namespace NCronJob;

/// <inheritdoc />
internal sealed record JobExecutionContext : IJobExecutionContext
{
    internal bool ExecuteChildren = true;

    /// <summary>
    /// Represents the context of a job execution. Marked internal to prevent external instantiation.
    /// </summary>
    /// <param name="jobRun">The Job Run.</param>
    internal JobExecutionContext(JobRun jobRun) => JobRun = jobRun;

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public object? Output { get; set; }

    /// <inheritdoc />
    public int Attempts { get; internal set; }

    /// <summary>The Job Run instance.</summary>
    internal JobRun JobRun { get; }

    /// <summary>The Type that represents the Job</summary>
    internal Type JobType => JobRun.JobDefinition.Type;

    /// <inheritdoc />
    public object? Parameter => JobRun.Parameter;

    /// <inheritdoc />
    public Guid CorrelationId => JobRun.CorrelationId;

    /// <inheritdoc />
    public object? ParentOutput => JobRun.ParentOutput;

    /// <inheritdoc />
    public void SkipChildren() => ExecuteChildren = false;
}
