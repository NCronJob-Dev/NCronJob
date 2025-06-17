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
}
