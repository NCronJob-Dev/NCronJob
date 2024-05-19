using LinkDotNet.NCronJob.Messaging.States;

namespace LinkDotNet.NCronJob;

public interface IJobExecutionContext
{
    /// <summary>
    /// The Job Instance Identifier, generated once upon creation of the context.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The attempts made to execute the job for one run. Will be incremented when a retry is triggered.
    /// Retries will only occur when <see cref="RetryPolicyAttribute{T}"/> is set on the Job.
    /// </summary>
    int Attempts { get; }

    DateTimeOffset ScheduledStartTime { get; set; }
    DateTimeOffset ActualStartTime { get; set; }
    DateTimeOffset? End { get; }
    TimeSpan? ExecutionTime { get; }
    ExecutionState CurrentState { get; set; }

    /// <summary>
    /// The output of a job that can be read by the <see cref="IJobNotificationHandler{TJob}"/>.
    /// </summary>
    object? Output { get; set; }

    internal IJobDefinition JobDefinition { get; init; }

    /// <summary>The Type that represents the Job</summary>
    Type JobType { get; }

    /// <summary>The passed in parameters to a job.</summary>
    object? Parameter { get; init; }
}
