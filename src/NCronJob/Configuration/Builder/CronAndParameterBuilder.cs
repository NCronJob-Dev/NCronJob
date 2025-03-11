namespace NCronJob;

/// <summary>
/// Represents a builder to refine a job with a cron expression or a parameter.
/// </summary>
public sealed class CronAndParameterBuilder : IOptionChainerBuilder
{
    private readonly JobOptionBuilder optionBuilder;
    private readonly JobOption jobOption;

    internal CronAndParameterBuilder(JobOptionBuilder optionBuilder, JobOption jobOption)
    {
        this.optionBuilder = optionBuilder;
        this.jobOption = jobOption;
    }

    /// <summary>
    /// Chains another option to the job.
    /// </summary>
    public JobOptionBuilder And => optionBuilder;

    /// <summary>
    /// The parameter that can be passed down to the job.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    /// <param name="parameter">The parameter to add that will be passed to the cron job.</param>
    /// <returns>Returns a <see cref="IOptionChainerBuilder"/> that allows chaining new options.</returns>
    public IOptionChainerBuilder WithParameter(object? parameter)
    {
        jobOption.Parameter = parameter;
        return this;
    }

    /// <summary>
    /// Adds a cron expression for the given job.
    /// </summary>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="timeZoneInfo">Optional, provides the timezone that is used to evaluate the cron expression. Defaults to UTC.</param>
    /// <returns>Returns a <see cref="ParameterOnlyBuilder"/> that allows naming the job or adding a parameter to it.</returns>
    public ParameterOnlyBuilder WithCronExpression(string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        jobOption.CronExpression = cronExpression;
        jobOption.TimeZoneInfo = timeZoneInfo;

        return new ParameterOnlyBuilder(optionBuilder, jobOption);
    }
}
