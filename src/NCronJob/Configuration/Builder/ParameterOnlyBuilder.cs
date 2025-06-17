namespace NCronJob;

/// <summary>
/// Represents a builder to configure a job with a parameter.
/// </summary>
public sealed class ParameterOnlyBuilder : OptionChainerBuilder
{
    private readonly JobOption jobOption;

    internal ParameterOnlyBuilder(
        JobOptionBuilder optionBuilder,
        JobOption jobOption):
        base(optionBuilder)
    {
        this.jobOption = jobOption;
    }

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
}
