using Cronos;

namespace LinkDotNet.NCronJob;

public interface IJobDefinition
{
    int JobExecutionCount { get; }
    Guid JobId { get; }
    CancellationToken CancellationToken { get; set; }
    string JobName { get; }

    /// <summary>
    /// The JobFullName is used as a unique identifier for the job type including anonymous jobs. This helps with concurrency management.
    /// </summary>
    string JobFullName { get; }

    Type Type { get; init; }
    object? Parameter { get; init; }
    CronExpression? CronExpression { get; init; }
    TimeZoneInfo? TimeZone { get; init; }
    JobPriority Priority { get; init; }
}
