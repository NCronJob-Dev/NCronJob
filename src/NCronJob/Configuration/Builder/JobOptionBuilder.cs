namespace NCronJob;

/// <summary>
/// Represents a builder to create jobs.
/// </summary>
public sealed class JobOptionBuilder
{
    private readonly List<JobOption> jobOptions = [];

    /// <summary>
    /// The jobOptions item we need to work with will always be the first.
    /// This is because we only support one job per builder.
    /// </summary>
    /// <returns></returns>
    internal JobOptionBuilder SetRunAtStartup()
    {
        if (jobOptions.Count == 1)
        {
            // Startup Jobs should not be initialized with a cron expression.
            if(jobOptions[0].CronExpression != null)
                throw new InvalidOperationException("Startup jobs cannot have a cron expression.");

            jobOptions[0].IsStartupJob = true;
        }
        return this;
    }

    /// <summary>
    /// Adds a cron expression for the given job.
    /// </summary>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    /// <param name="timeZoneInfo">Optional, provides the timezone that is used to evaluate the cron expression. Defaults to UTC.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows adding parameters to the job.</returns>
    public ParameterBuilder WithCronExpression(string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        cronExpression = cronExpression.Trim();

        var jobOption = new JobOption
        {
            CronExpression = cronExpression,
            TimeZoneInfo = timeZoneInfo ?? TimeZoneInfo.Utc
        };

        jobOptions.Add(jobOption);

        return new ParameterBuilder(this, jobOption);
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
