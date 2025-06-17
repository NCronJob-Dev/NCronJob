namespace NCronJob;

/// <summary>
/// Represents a builder to create jobs.
/// </summary>
public sealed class JobOptionBuilder
{
    private readonly List<JobOption> jobOptions = [];

    /// <summary>
    /// Adds a cron expression for the given job.
    /// </summary>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="timeZoneInfo">Optional, provides the timezone that is used to evaluate the cron expression. Defaults to UTC.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows naming the job or adding a parameter to it.</returns>
    public ParameterBuilder WithCronExpression(string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        var jobOption = new JobOption
        {
            CronExpression = cronExpression,
            TimeZoneInfo = timeZoneInfo
        };

        jobOptions.Add(jobOption);

        return new ParameterBuilder(this, jobOption);
    }

    /// <summary>
    /// Sets the job name. This can be used to identify the job.
    /// </summary>
    /// <param name="jobName">The job name associated with this job.</param>
    /// <returns>Returns a <see cref="CronAndParameterAndRunAtStartupBuilder"/> that allows further configuration.</returns>
    /// <remarks>The job name should be unique over all job instances.</remarks>
    public CronAndParameterAndRunAtStartupBuilder WithName(string jobName)
    {
        var jobOption = new JobOption
        {
            Name = jobName,
        };

        jobOptions.Add(jobOption);

        return new CronAndParameterAndRunAtStartupBuilder(this, jobOption);
    }

    /// <summary>
    /// The parameter that can be passed down to the job.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    /// <param name="parameter">The parameter to add that will be passed to the cron job.</param>
    /// <returns>Returns a <see cref="RunAtStartupBuilder"/> that allows configuring the job to run at startup.</returns>
    public RunAtStartupBuilder WithParameter(object? parameter)
    {
        var jobOption = new JobOption
        {
            Parameter = parameter,
        };

        jobOptions.Add(jobOption);

        return new RunAtStartupBuilder(this, jobOption);
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
        var jobOption = new JobOption
        {
            ShouldCrashOnStartupFailure = shouldCrashOnFailure,
        };

        jobOptions.Add(jobOption);

        return new OptionChainerBuilder(this);
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
