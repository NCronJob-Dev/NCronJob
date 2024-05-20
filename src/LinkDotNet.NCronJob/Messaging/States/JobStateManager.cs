using System.Collections.Concurrent;

namespace LinkDotNet.NCronJob.Messaging.States;

/// <summary>
/// 
/// </summary>
/// <param name="messageHub"></param>
internal sealed class JobStateManager(IMessageHub messageHub): IMonitorStateCronJobs
{
    /// <summary>
    /// 
    /// </summary>
    public event Func<JobStateMessage, Task>? JobStatusChanged;

    private readonly ConcurrentDictionary<Guid, ExecutionState> currentStates = new();


    /// <summary>
    /// 
    /// </summary>
    /// <param name="jobContext"></param>
    /// <param name="newState"></param>
    /// <param name="status"></param>
    public async Task SetState(JobExecutionContext jobContext, ExecutionState newState, JobRunStatus? status = null)
    {
        switch (newState)
        {
            //TODO: Need State Machine to replace this logic
            case ExecutionState.Executing:
                jobContext.ActualStartTime = DateTimeOffset.Now;
                // TODO: jobEntry needs a redesign to allow for mutating objects
                //jobEntry = jobEntry with { JobContext = jobEntry.JobContext with { ActualStartTime = DateTimeOffset.Now } };
                break;
            case ExecutionState.Completed:
                jobContext.End = DateTimeOffset.Now;
                break;
        }

        var id = jobContext.JobDefinition.JobId;
        if (currentStates.TryGetValue(id, out var existingState))
        {
            // If the state exists and is different, update and notify
            if (existingState != newState)
            {
                currentStates[id] = newState;
                jobContext.CurrentState = newState;
                await NotifyStateChange(new JobStateMessage(jobContext, newState, status));
            }
        }
        else
        {
            // If the state does not exist, add it and notify as this is the first setting
            currentStates[id] = newState;
            jobContext.CurrentState = newState;
            await NotifyStateChange(new JobStateMessage(jobContext, newState, status));
        }
    }

    private async Task NotifyStateChange(JobStateMessage message)
    {
        await messageHub.PublishAsync(message);
        if (JobStatusChanged is not null)
        {
            foreach (var handler in JobStatusChanged.GetInvocationList().Cast<Func<JobStateMessage, Task>>())
            {
                try
                {
                    await handler(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in handling state change by one of the subscribers: {ex}");
                }
            }
        }
    }
}
