namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents a builder to create parameters for a job.
/// </summary>
public sealed class ParameterBuilder
{
    private readonly JobOptionBuilder optionBuilder;
    private readonly JobOption jobOption;

    internal ParameterBuilder(JobOptionBuilder optionBuilder, JobOption jobOption)
    {
        this.optionBuilder = optionBuilder;
        this.jobOption = jobOption;
    }

    /// <summary>
    /// The parameter that can be passed down to the job. This only applies to cron jobs.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    /// <param name="parameter">The paramter to add that will be passed to the cron job.</param>
    /// <returns>Returns a <see cref="JobOptionBuilder"/> that allows adding more options (like additional cron defintions) to the job.</returns>
    /// <remarks>
    /// Calling this method multiple times on the same cron expression, will overwrite the last set value.
    /// Therefore:
    /// <code>
    /// p => p.WithCronExpression("* * * * *").WithParameter("first").WithParameter("second")
    /// </code>
    /// Will result in the parameter "second" being passed to the job.
    /// To pass multiple parameters, create multiple cron expressions with the same value:
    /// <code>
    /// p => p.WithCronExpression("* * * * *").WithParameter("first").WithCronExpression("* * * * *").WithParameter("second")
    /// </code>
    /// </remarks>
    public JobOptionBuilder WithParameter(object? parameter)
    {
        jobOption.Parameter = parameter;
        return optionBuilder;
    }

    /// <summary>
    /// Adds a cron expression for the given job.
    /// </summary>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="enableSecondPrecision">If set to <c>true</c>, the cron expression can specify second-level precision.</param>
    /// <returns>Returns a <see cref="JobOptionBuilder"/> that allows adding parameters to the job.</returns>
    public ParameterBuilder WithCronExpression(string cronExpression, bool enableSecondPrecision = false)
        => optionBuilder.WithCronExpression(cronExpression, enableSecondPrecision);
}
