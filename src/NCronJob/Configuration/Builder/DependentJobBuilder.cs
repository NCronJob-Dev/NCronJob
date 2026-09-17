namespace NCronJob;

/// <summary>
/// Represents a builder for configuring dependent jobs that run after a parent job completes.
/// </summary>
public sealed class DependentJobBuilder
{
    private readonly DependencyBuilder dependencyBuilder;
    private readonly DependentJobDefinition jobDefinition;
    private JobOption? jobOption;

    internal DependentJobBuilder(DependencyBuilder dependencyBuilder, DependentJobDefinition jobDefinition)
    {
        this.dependencyBuilder = dependencyBuilder;
        this.jobDefinition = jobDefinition;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Func{bool})"/>
    /// <returns>Returns the same <see cref="DependentJobBuilder"/> that allows further configuration.</returns>
    public DependentJobBuilder OnlyIf(Func<bool> predicate)
    {
        EnsureJobOption().AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Delegate)"/>
    /// <returns>Returns the same <see cref="DependentJobBuilder"/> that allows further configuration.</returns>
    public DependentJobBuilder OnlyIf(Delegate predicate)
    {
        EnsureJobOption().AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Func{Task{bool}})"/>
    /// <returns>Returns the same <see cref="DependentJobBuilder"/> that allows further configuration.</returns>
    public DependentJobBuilder OnlyIf(Func<Task<bool>> predicate)
    {
        EnsureJobOption().AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.WithTimeout(TimeSpan)"/>
    public DependentJobBuilder WithTimeout(TimeSpan timeout)
    {
        EnsureJobOption().SetTimeout(timeout);
        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.WithJobRunExpiry(TimeSpan)"/>
    public DependentJobBuilder WithJobRunExpiry(TimeSpan expiry)
    {
        EnsureJobOption().SetJobRunExpiry(expiry);
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
            jobOption = null; // Reset to prevent duplicate application
        }
    }

    private JobOption EnsureJobOption() => jobOption ??= new JobOption();
}
