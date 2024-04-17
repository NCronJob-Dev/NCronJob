using System.Diagnostics.CodeAnalysis;

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
    /// <param name="timeZoneInfo">Optional, provides the timezone that is used to evaluate the cron expression. Defaults to UTC.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows adding parameters to the job.</returns>
    public ParameterBuilder WithCronExpression(string cronExpression, bool enableSecondPrecision = false, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        cronExpression = cronExpression.Trim();
        if (!IsValidCronExpression(cronExpression, enableSecondPrecision))
        {
            throw new ArgumentException($"Invalid cron expression format for {(enableSecondPrecision ? "second precision" : "minute precision")}.");
        }

        var jobOption = new JobOption
        {
            CronExpression = cronExpression.Trim(),
            EnableSecondPrecision = enableSecondPrecision,
            TimeZoneInfo = timeZoneInfo ?? TimeZoneInfo.Utc
        };

        jobOptions.Add(jobOption);

        return new ParameterBuilder(this, jobOption);
    }

    private static bool IsValidCronExpression(string cronExpression, bool enableSecondPrecision)
    {
        var parts = cronExpression.Split(' ');
        return (enableSecondPrecision && parts.Length == 6) || (!enableSecondPrecision && parts.Length == 5);
    }

    internal List<JobOption> GetJobOptions() => jobOptions;
}
