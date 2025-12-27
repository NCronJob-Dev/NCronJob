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

    /// <summary>
    /// Adds a condition that must be satisfied for the job to execute.
    /// Multiple conditions are combined with AND logic - all must return true.
    /// </summary>
    /// <param name="predicate">A synchronous predicate that returns true if the job should execute.</param>
    /// <returns>Returns a <see cref="RunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public RunAtStartupBuilder OnlyIf(Func<bool> predicate)
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
    /// <returns>Returns a <see cref="RunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// Example:
    /// <code>
    /// .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("my-job"))
    /// </code>
    /// </remarks>
    public RunAtStartupBuilder OnlyIf(Delegate predicate)
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
    /// <returns>Returns a <see cref="RunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public RunAtStartupBuilder OnlyIf(Func<Task<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        jobOption.Conditions ??= [];
        jobOption.Conditions.Add(async (_, ct) => await predicate().ConfigureAwait(false));
        
        return this;
    }
}
