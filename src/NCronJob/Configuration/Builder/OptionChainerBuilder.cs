namespace NCronJob;

/// <summary>
/// Represents a builder to chain options.
/// </summary>
public class OptionChainerBuilder : IOptionChainerBuilder
{
    /// <summary>
    /// The option builder that is used to chain options together.
    /// </summary>
    protected JobOptionBuilder OptionBuilder { get; }

    internal OptionChainerBuilder(JobOptionBuilder optionBuilder)
    {
        OptionBuilder = optionBuilder;
    }

    /// <inheritdoc />
    public JobOptionBuilder And => OptionBuilder;
}
