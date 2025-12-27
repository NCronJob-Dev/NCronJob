namespace NCronJob;

/// <summary>
/// Represents a builder to configure a job with a name or a parameter.
/// </summary>
public sealed class ParameterBuilder : OptionChainerBuilder
{
    private readonly JobOption jobOption;

    internal ParameterBuilder(
        JobOptionBuilder optionBuilder,
        JobOption jobOption)
        : base(optionBuilder)
    {
        this.jobOption = jobOption;
    }

    /// <summary>
    /// The parameter that can be passed down to the job. This only applies to cron jobs.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    /// <param name="parameter">The parameter to add that will be passed to the cron job.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// Calling this method multiple times on the same cron expression, will overwrite the last set value.
    /// Therefore:
    /// <code>
    /// p => p.WithCronExpression("* * * * *").WithParameter("first").WithParameter("second")
    /// </code>
    /// Will result in the parameter "second" being passed to the job.
    /// To pass multiple parameters, create multiple cron expressions with the same value:
    /// <code>
    /// p => p.WithCronExpression("* * * * *").WithParameter("first").And.WithCronExpression("* * * * *").WithParameter("second")
    /// </code>
    /// </remarks>
    public ParameterBuilder WithParameter(object? parameter)
    {
        jobOption.Parameter = parameter;
        return this;
    }

    /// <summary>
    /// Sets the job name. This can be used to identify the job.
    /// </summary>
    /// <param name="jobName">The job name associated with this job.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    /// <remarks>The job name should be unique over all job instances.</remarks>
    public ParameterBuilder WithName(string jobName)
    {
        jobOption.Name = jobName;
        return this;
    }

    /// <summary>
    /// Adds a condition that must be satisfied for the job to execute.
    /// Multiple conditions are combined with AND logic - all must return true.
    /// </summary>
    /// <param name="predicate">A synchronous predicate that returns true if the job should execute.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public ParameterBuilder OnlyIf(Func<bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        jobOption.Conditions ??= [];
        jobOption.Conditions.Add((_, _) => new ValueTask<bool>(predicate()));
        
        return this;
    }

    /// <summary>
    /// Adds a condition that must be satisfied for the job to execute, with dependency injection support.
    /// Multiple conditions are combined with AND logic - all must return true.
    /// </summary>
    /// <param name="predicate">A delegate that accepts dependencies from DI and returns true if the job should execute.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// Example:
    /// <code>
    /// .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("my-job"))
    /// </code>
    /// </remarks>
    public ParameterBuilder OnlyIf(Delegate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        var invoker = ConditionInvokerBuilder.BuildConditionInvoker(predicate);
        
        jobOption.Conditions ??= [];
        jobOption.Conditions.Add(invoker);
        
        return this;
    }

    /// <summary>
    /// Adds an asynchronous condition that must be satisfied for the job to execute.
    /// Multiple conditions are combined with AND logic - all must return true.
    /// </summary>
    /// <param name="predicate">An asynchronous predicate that returns true if the job should execute.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public ParameterBuilder OnlyIf(Func<Task<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        jobOption.Conditions ??= [];
        jobOption.Conditions.Add(async (_, ct) => await predicate().ConfigureAwait(false));
        
        return this;
    }
}
