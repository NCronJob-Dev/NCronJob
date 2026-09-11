namespace NCronJob;

/// <summary>
/// Represents a builder to configure a startup job.
/// </summary>
public sealed class RunAtStartupBuilder : OptionChainerBuilder
{
    private readonly JobOption jobOption;

    internal RunAtStartupBuilder(
        JobOptionBuilder optionBuilder,
        JobOption jobOption)
        : base(optionBuilder)
    {
        this.jobOption = jobOption;
    }

    /// <summary>
    /// Configures the job to run once before the application itself runs.
    /// </summary>
    /// <param name="shouldCrashOnFailure">When <code>false</code>, will ignore any exception and allow the the application to start would the job crash. Default is <code>true</code>.</param>
    /// <remarks>
    /// If a job is marked to run at startup, it will be executed before any `IHostedService` is started.
    /// All startup jobs will be executed (and awaited) before the web application is started. This is particular useful for migration and cache hydration.
    /// </remarks>
    /// <returns>Returns a <see cref="IOptionChainerBuilder"/> that allows chaining new options.</returns>
    public IOptionChainerBuilder RunAtStartup(bool shouldCrashOnFailure = true)
    {
        jobOption.ShouldCrashOnStartupFailure = shouldCrashOnFailure;
        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Func{bool})"/>
    /// <returns>Returns the same <see cref="RunAtStartupBuilder"/> that allows further configuration.</returns>
    public RunAtStartupBuilder OnlyIf(Func<bool> predicate)
    {
        jobOption.AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Delegate)"/>
    /// <returns>Returns the same <see cref="RunAtStartupBuilder"/> that allows further configuration.</returns>
    public RunAtStartupBuilder OnlyIf(Delegate predicate)
    {
        jobOption.AddCondition(predicate);

        return this;
    }

    /// <inheritdoc cref="JobOptionBuilder.OnlyIf(Func{Task{bool}})"/>
    /// <returns>Returns the same <see cref="RunAtStartupBuilder"/> that allows further configuration.</returns>
    public RunAtStartupBuilder OnlyIf(Func<Task<bool>> predicate)
    {
        jobOption.AddCondition(predicate);

        return this;
    }
}
