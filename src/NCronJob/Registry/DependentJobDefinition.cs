namespace NCronJob;

internal sealed class DependentJobDefinition
{
    private readonly Delegate? jobDelegate;
    private readonly List<JobOption> jobOptions = [];
    private readonly JobExecutionAttributes jobPolicyMetadata;

    private DependentJobDefinition(string? customName, Type type, object? parameter)
    {
        CustomName = customName;
        Type = type;
        Parameter = parameter;
        IsTypedJob = true;
        jobPolicyMetadata = new JobExecutionAttributes(type);
    }

    private DependentJobDefinition(string? customName, Delegate jobDelegate)
    {
        CustomName = customName;
        this.jobDelegate = jobDelegate;
        jobPolicyMetadata = new JobExecutionAttributes(jobDelegate);
    }

    public string? CustomName { get; }

    public Type? Type { get; }

    public object? Parameter { get; }

    public bool IsTypedJob { get; }

    public SupportsConcurrencyAttribute? ConcurrencyPolicy => jobPolicyMetadata.ConcurrencyPolicy;

    public string Name => ToJobDefinition().Name;

    public static DependentJobDefinition CreateTyped(Type type, object? parameter, string? customName = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.FullName is null || !type.GetInterfaces().Contains(typeof(IJob))
            ? throw new InvalidOperationException($"Type '{type}' doesn't implement '{nameof(IJob)}'.")
            : new DependentJobDefinition(customName, type, parameter);
    }

    public static DependentJobDefinition CreateUntyped(string? customName, Delegate jobDelegate)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);
        return new DependentJobDefinition(customName, jobDelegate);
    }

    public void UpdateWith(JobOption jobOption)
    {
        ArgumentNullException.ThrowIfNull(jobOption);
        jobOptions.Add(jobOption);
    }

    public JobDefinition ToJobDefinition()
    {
        var jobDefinition = IsTypedJob
            ? JobDefinition.CreateTyped(CustomName, Type!, Parameter)
            : JobDefinition.CreateUntyped(CustomName, jobDelegate!);
        jobDefinition.MarkAsDependent();

        foreach (var jobOption in jobOptions)
        {
            jobDefinition.UpdateWith(jobOption);
        }

        return jobDefinition;
    }
}
