namespace NCronJob;

/// <summary>
/// Represents a builder to create jobs.
/// </summary>
public sealed class JobOptionBuilder
{
    private readonly List<JobOption> jobOptions = [];

    /// <summary>
    /// Configures the maximum execution time for the job.
    /// </summary>
    /// <param name="timeout">A positive duration, or <see cref="Timeout.InfiniteTimeSpan"/> for no timeout.</param>
    /// <returns>A builder that allows further configuration of this job.</returns>
    public CronAndParameterAndRunAtStartupBuilder WithTimeout(TimeSpan timeout)
    {
        return new CronAndParameterAndRunAtStartupBuilder(this, AddOption(o => o.SetTimeout(timeout)));
    }

    /// <summary>
    /// Overrides how long a scheduled run may remain queued after its intended run time before expiring.
    /// </summary>
    /// <param name="expiry">A positive duration, or <see cref="Timeout.InfiniteTimeSpan"/> to disable expiry.</param>
    /// <returns>A builder that allows further configuration of this job.</returns>
    public CronAndParameterAndRunAtStartupBuilder WithJobRunExpiry(TimeSpan expiry)
    {
        return new CronAndParameterAndRunAtStartupBuilder(this, AddOption(o => o.SetJobRunExpiry(expiry)));
    }

    /// <summary>
    /// Adds a cron expression for the given job.
    /// </summary>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="timeZoneInfo">Optional, provides the timezone that is used to evaluate the cron expression. Defaults to UTC.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows naming the job or adding a parameter to it.</returns>
    public ParameterBuilder WithCronExpression(string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        return new ParameterBuilder(this, AddOption(o =>
        {
            o.CronExpression = cronExpression;
            o.TimeZoneInfo = timeZoneInfo;
        }));
    }

    /// <summary>
    /// Sets the job name. This can be used to identify the job.
    /// </summary>
    /// <param name="jobName">The job name associated with this job.</param>
    /// <returns>Returns a <see cref="CronAndParameterAndRunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>The job name should be unique over all job instances.</remarks>
    public CronAndParameterAndRunAtStartupBuilder WithName(string jobName)
    {
        return new CronAndParameterAndRunAtStartupBuilder(this, AddOption(o => o.Name = jobName));
    }

    /// <summary>
    /// The parameter that can be passed down to the job.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    /// <param name="parameter">The parameter to add that will be passed to the cron job.</param>
    /// <returns>Returns a <see cref="RunAtStartupBuilder"/> that allows configuring the job to run at startup.</returns>
    public RunAtStartupBuilder WithParameter(object? parameter)
    {
        return new RunAtStartupBuilder(this, AddOption(o => o.Parameter = parameter));
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
        return new RunAtStartupBuilder(this, AddOption(o => o.ShouldCrashOnStartupFailure = shouldCrashOnFailure));
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
        return new CronAndParameterAndRunAtStartupBuilder(this, AddOption(o => o.AddCondition(predicate)));
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
        return new CronAndParameterAndRunAtStartupBuilder(this, AddOption(o => o.AddCondition(predicate)));
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
        return new CronAndParameterAndRunAtStartupBuilder(this, AddOption(o => o.AddCondition(predicate)));
    }

    private JobOption AddOption(Action<JobOption> configure)
    {
        var jobOption = new JobOption();
        configure(jobOption);
        jobOptions.Add(jobOption);
        return jobOption;
    }

    internal List<JobOption> GetJobOptions()
    {
        if (jobOptions.Count == 0)
        {
            jobOptions.Add(new JobOption());
        }

        return jobOptions;
    }
}
