using System.Collections.Concurrent;
using System.Reflection;

namespace NCronJob;

/// <inheritdoc />
internal class JobExecutionContext : IJobExecutionContext
{
    private static readonly ConcurrentDictionary<Type, Func<JobRun, JobExecutionContext>> TypedFactories = new();

    internal bool ExecuteChildren = true;

    public JobExecutionContext(JobRun jobRun)
    {
        JobRun = jobRun;
    }

    public static JobExecutionContext Create(JobRun jobRun) =>
        jobRun.JobDefinition.ParameterType is { } parameterType
            ? TypedFactories.GetOrAdd(parameterType, CreateTypedFactory)(jobRun)
            : new JobExecutionContext(jobRun);

    private static Func<JobRun, JobExecutionContext> CreateTypedFactory(Type parameterType) =>
        typeof(JobExecutionContext)
            .GetMethod(nameof(CreateTyped), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(parameterType)
            .CreateDelegate<Func<JobRun, JobExecutionContext>>();

    public static JobExecutionContext<TParameter> CreateTyped<TParameter>(JobRun jobRun) => new(jobRun);

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
    public Guid CorrelationId => JobRun.CorrelationId;

    /// <inheritdoc />
    public object? ParentOutput => JobRun.ParentOutput;

    /// <inheritdoc />
    public void SkipChildren() => ExecuteChildren = false;
}

/// <inheritdoc cref="IJobExecutionContext{TParameter}" />
internal sealed class JobExecutionContext<TParameter> : JobExecutionContext, IJobExecutionContext<TParameter>
{
    public JobExecutionContext(JobRun jobRun)
        : base(jobRun)
    {
    }

    /// <inheritdoc />
    public TParameter Parameter => JobRun.Parameter is TParameter parameter ? parameter : default!;
}
