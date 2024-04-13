namespace LinkDotNet.NCronJob;

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
    /// <param name="enableSecondPrecision">If set to <c>true</c>, the cron expression can specify second-level precision.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows adding parameters to the job.</returns>
    public ParameterBuilder WithCronExpression(string cronExpression, bool enableSecondPrecision = false)
    {
        var jobOption = new JobOption
        {
            CronExpression = cronExpression,
            EnableSecondPrecision = enableSecondPrecision
        };

        jobOptions.Add(jobOption);

        return new ParameterBuilder(this, jobOption);
    }

    internal List<JobOption> GetJobOptions() => jobOptions;
}
