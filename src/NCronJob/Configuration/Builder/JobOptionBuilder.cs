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
    /// <returns>Returns a <see cref="CronAndParameterBuilder"/> that allows further configuration.</returns>
    /// <remarks>The job name should be unique over all job instances.</remarks>
    public CronAndParameterBuilder WithName(string jobName)
    {
        var jobOption = new JobOption
        {
            Name = jobName,
        };

        jobOptions.Add(jobOption);

        return new CronAndParameterBuilder(this, jobOption);
    }

    /// <summary>
    /// The parameter that can be passed down to the job.<br/>
    /// When an instant job is triggered a parameter can be passed down via the <see cref="IInstantJobRegistry"/> interface.
    /// </summary>
    /// <param name="parameter">The parameter to add that will be passed to the cron job.</param>
    /// <returns>Returns a <see cref="IOptionChainerBuilder"/> that allows chaining new options.</returns>
    public IOptionChainerBuilder WithParameter(object? parameter)
    {
        var jobOption = new JobOption
        {
            Parameter = parameter,
        };

        jobOptions.Add(jobOption);
        return new ParameterOnlyBuilder(this, jobOption);
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
