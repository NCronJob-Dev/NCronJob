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
    /// <param name="enableSecondPrecision">
    /// Specifies whether the cron expression should consider second-level precision.
    /// This parameter is optional. If not provided, or set to null, it auto-detects based on the number
    /// of parts in the cron expression (6 parts indicate second-level precision, otherwise minute-level precision).
    /// </param>
    /// <param name="timeZoneInfo">Optional, provides the timezone that is used to evaluate the cron expression. Defaults to UTC.</param>
    /// <returns>Returns a <see cref="ParameterBuilder"/> that allows adding parameters to the job.</returns>
    public ParameterBuilder WithCronExpression([StringSyntax(StringSyntaxAttribute.Regex)]string cronExpression, bool? enableSecondPrecision = null, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(cronExpression);

        cronExpression = cronExpression.Trim();
        var determinedPrecision = DetermineAndValidatePrecision(cronExpression, enableSecondPrecision);

        var jobOption = new JobOption
        {
            CronExpression = cronExpression,
            EnableSecondPrecision = determinedPrecision,
            TimeZoneInfo = timeZoneInfo ?? TimeZoneInfo.Utc
        };

        jobOptions.Add(jobOption);

        return new ParameterBuilder(this, jobOption);
    }


    internal static bool DetermineAndValidatePrecision(string cronExpression, bool? enableSecondPrecision)
    {
        var parts = cronExpression.Split(' ');
        var precisionRequired = enableSecondPrecision ?? (parts.Length == 6);

        var expectedLength = precisionRequired ? 6 : 5;
        if (parts.Length != expectedLength)
        {
            var precisionText = precisionRequired ? "second precision" : "minute precision";
            throw new ArgumentException($"Invalid cron expression format for {precisionText}.", nameof(cronExpression));
        }

        return precisionRequired;
    }

    internal List<JobOption> GetJobOptions() => jobOptions.Count > 0 ? jobOptions : [new JobOption()];
}
