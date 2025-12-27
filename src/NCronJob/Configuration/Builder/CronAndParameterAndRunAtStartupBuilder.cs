namespace NCronJob;

/// <summary>
/// Represents a builder to refine a job with a cron expression or a parameter or a startup trigger.
/// </summary>
public sealed class CronAndParameterAndRunAtStartupBuilder : OptionChainerBuilder
{
    private readonly JobOption jobOption;

    internal CronAndParameterAndRunAtStartupBuilder(
        JobOptionBuilder optionBuilder,
        JobOption jobOption)
        : base(optionBuilder)
    {
        this.jobOption = jobOption;
    }

    /// <summary>
    /// Adds a cron expression for the given job.
    /// </summary>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="timeZoneInfo">Optional, provides the timezone that is used to evaluate the cron expression. Defaults to UTC.</param>
    /// <returns>Returns a <see cref="ParameterAndRunAtStartupBuilder"/> that allows naming the job or adding a parameter to it.</returns>
    public ParameterAndRunAtStartupBuilder WithCronExpression(string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        jobOption.CronExpression = cronExpression;
        jobOption.TimeZoneInfo = timeZoneInfo;

        return new ParameterAndRunAtStartupBuilder(OptionBuilder, jobOption);
    }

    /// <summary>
    /// The parameter that can be passed down to the job.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    /// <param name="parameter">The parameter to add that will be passed to the cron job.</param>
    /// <returns>Returns a <see cref="IOptionChainerBuilder"/> that allows chaining new options.</returns>
    public RunAtStartupBuilder WithParameter(object? parameter)
    {
        jobOption.Parameter = parameter;

        return new RunAtStartupBuilder(OptionBuilder, jobOption);
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
    /// <returns>Returns a <see cref="CronAndParameterAndRunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public CronAndParameterAndRunAtStartupBuilder OnlyIf(Func<bool> predicate)
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
    /// <returns>Returns a <see cref="CronAndParameterAndRunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// Example:
    /// <code>
    /// .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("my-job"))
    /// </code>
    /// </remarks>
    public CronAndParameterAndRunAtStartupBuilder OnlyIf(Delegate predicate)
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
    /// <returns>Returns a <see cref="CronAndParameterAndRunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>
    /// The condition is evaluated once before job instantiation. If it returns false, the job is skipped.
    /// Conditions are NOT re-evaluated during retry attempts - if the initial condition was true, retries proceed.
    /// Multiple OnlyIf calls are combined with AND logic.
    /// </remarks>
    public CronAndParameterAndRunAtStartupBuilder OnlyIf(Func<Task<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        jobOption.Conditions ??= [];
        jobOption.Conditions.Add(async (_, ct) => await predicate().ConfigureAwait(false));
        
        return this;
    }
}
