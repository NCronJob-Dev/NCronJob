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

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Func{bool})"/>
    /// <returns>Returns the same <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    public ParameterBuilder OnlyIf(Func<bool> predicate)
    {
        jobOption.AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Delegate)"/>
    /// <returns>Returns the same <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    public ParameterBuilder OnlyIf(Delegate predicate)
    {
        jobOption.AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Func{Task{bool}})"/>
    /// <returns>Returns the same <see cref="ParameterBuilder"/> that allows further configuration.</returns>
    public ParameterBuilder OnlyIf(Func<Task<bool>> predicate)
    {
        jobOption.AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.WithTimeout(TimeSpan)"/>
    public ParameterBuilder WithTimeout(TimeSpan timeout)
    {
        jobOption.SetTimeout(timeout);
        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.WithJobRunExpiry(TimeSpan)"/>
    public ParameterBuilder WithJobRunExpiry(TimeSpan expiry)
    {
        jobOption.SetJobRunExpiry(expiry);
        return this;
    }
}
