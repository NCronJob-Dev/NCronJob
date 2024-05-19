using LinkDotNet.NCronJob.Messaging.States;

namespace LinkDotNet.NCronJob.Shared;

public record JobDetailsView
{
    public Guid JobId { get; init; }
    public string JobName { get; init; }
    public string JobTypeFullName { get; set; }
    public string? CronExpression { get; init; }
    public DateTimeOffset? NextRunTime { get; init; }
    public DateTimeOffset? LastRunTime { get; set; }
    public DateTimeOffset? CurrentRunStartTime { get; init; }
    public int ExecutionCount { get; init; }
    public bool JobIsRunning => CurrentState is ExecutionState.Executing or ExecutionState.Retrying;
    public ExecutionState CurrentState { get; set; } = ExecutionState.NotStarted;

    public bool IsCancellable { get; init; }
    public bool IsPaused { get; init; }

    public JobDetailsView() // Parameterless constructor for deserialization
    {
        JobName = "jobName";
        JobTypeFullName = "jobTypeFullName";
    }

    internal JobDetailsView(IJobExecutionContext ctx)
    {
        var registryEntry = ctx.JobDefinition;
        JobId = registryEntry.JobId;
        JobName = registryEntry.JobName;
        JobTypeFullName = registryEntry.JobFullName ?? string.Empty;
        CronExpression = registryEntry.CronExpression?.ToString();
        ExecutionCount = registryEntry.JobExecutionCount;
        CurrentState = ctx.CurrentState;
        NextRunTime = registryEntry.CronExpression?.GetNextOccurrence(DateTime.UtcNow);  // TODO: this needs to be recorded from within the ConScheduler
        CurrentRunStartTime = ctx.ActualStartTime;
        LastRunTime = CurrentRunStartTime;  // TODO: this needs basic in memory persistence

        // testing
        IsPaused = !IsPaused;
        IsCancellable = !IsCancellable;
    }
}
