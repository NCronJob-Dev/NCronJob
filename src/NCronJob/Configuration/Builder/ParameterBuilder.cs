namespace NCronJob;

/// <summary>
/// Represents a builder to create parameters for a job.
/// </summary>
public sealed class ParameterBuilder
{
    private readonly JobOption jobOption;

    /// <summary>
    /// Chains another cron expression to the job.
    /// </summary>
    public JobOptionBuilder And { get; }

    internal ParameterBuilder(JobOptionBuilder optionBuilder, JobOption jobOption)
    {
        And = optionBuilder;
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
}
