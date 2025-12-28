namespace NCronJob;

/// <summary>
/// Represents a builder for configuring dependent jobs that run after a parent job completes.
/// </summary>
public sealed class DependentJobBuilder
{
    private readonly DependencyBuilder dependencyBuilder;
    private readonly JobDefinition jobDefinition;
    private JobOption? jobOption;

    internal DependentJobBuilder(DependencyBuilder dependencyBuilder, JobDefinition jobDefinition)
    {
        this.dependencyBuilder = dependencyBuilder;
        this.jobDefinition = jobDefinition;
    }

    /// <summary>
    /// Adds a condition that must be satisfied for the dependent job to execute.
    /// Multiple conditions are combined with AND logic - all must return true.
    /// </summary>
    /// <param name="predicate">A synchronous predicate that returns true if the job should execute.</param>
    /// <returns>Returns a <see cref="DependentJobBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public DependentJobBuilder OnlyIf(Func<bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        EnsureJobOption();
        jobOption!.Conditions ??= [];
        jobOption.Conditions.Add((_, _) => new ValueTask<bool>(predicate()));
        
        return this;
    }

    /// <summary>
    /// Adds a condition that must be satisfied for the dependent job to execute, with dependency injection support.
    /// Multiple conditions are combined with AND logic - all must return true.
    /// </summary>
    /// <param name="predicate">A delegate that accepts dependencies from DI and returns true if the job should execute.</param>
    /// <returns>Returns a <see cref="DependentJobBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// Example:
    /// <code>
    /// .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("my-job"))
    /// </code>
    /// </remarks>
    public DependentJobBuilder OnlyIf(Delegate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        var invoker = ConditionInvokerBuilder.BuildConditionInvoker(predicate);
        
        EnsureJobOption();
        jobOption!.Conditions ??= [];
        jobOption.Conditions.Add(invoker);
        
        return this;
    }

    /// <summary>
    /// Adds an asynchronous condition that must be satisfied for the dependent job to execute.
    /// Multiple conditions are combined with AND logic - all must return true.
    /// </summary>
    /// <param name="predicate">An asynchronous predicate that returns true if the job should execute.</param>
    /// <returns>Returns a <see cref="DependentJobBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public DependentJobBuilder OnlyIf(Func<Task<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        EnsureJobOption();
        jobOption!.Conditions ??= [];
        jobOption.Conditions.Add(async (_, ct) => await predicate().ConfigureAwait(false));
        
        return this;
    }

    /// <summary>
    /// Adds another job that runs after the principal job has finished.
    /// </summary>
    /// <typeparam name="TJob">The type of the job to run.</typeparam>
    /// <param name="parameter">Optional parameter to pass to the job.</param>
    /// <returns>Returns a <see cref="DependentJobBuilder"/> for the newly added job.</returns>
    public DependentJobBuilder RunJob<TJob>(object? parameter = null)
        where TJob : IJob
    {
        ApplyJobOption();
        return dependencyBuilder.RunJob<TJob>(parameter);
    }

    /// <summary>
    /// Adds an anonymous delegate job that runs after the principal job has finished.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed. This delegate must return either void or Task.</param>
    /// <param name="jobName">Sets the job name that can be used to identify and manipulate the job later on.</param>
    /// <returns>Returns a <see cref="DependentJobBuilder"/> for the newly added job.</returns>
    public DependentJobBuilder RunJob(Delegate jobDelegate, string? jobName = null)
    {
        ApplyJobOption();
        return dependencyBuilder.RunJob(jobDelegate, jobName);
    }

    internal void ApplyJobOption()
    {
        if (jobOption is not null)
        {
            jobDefinition.UpdateWith(jobOption);
        }
    }

    private void EnsureJobOption()
    {
        jobOption ??= new JobOption();
    }
}
