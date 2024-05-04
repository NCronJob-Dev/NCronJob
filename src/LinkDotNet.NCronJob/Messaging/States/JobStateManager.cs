using System.Collections.Concurrent;

namespace LinkDotNet.NCronJob.Messaging.States;

/// <summary>
/// 
/// </summary>
/// <param name="messageHub"></param>
public sealed class JobStateManager(IMessageHub messageHub)
{
    /// <summary>
    /// 
    /// </summary>
    public event Func<JobStateMessage, Task>? StateChangedAsync;

    private readonly ConcurrentDictionary<Guid, ExecutionState> currentStates = new();


    /// <summary>
    /// 
    /// </summary>
    /// <param name="jobContext"></param>
    /// <param name="newState"></param>
    /// <param name="status"></param>
    public async Task SetState(JobExecutionContext jobContext, ExecutionState newState, JobRunStatus? status = null)
    {
        if (currentStates.TryGetValue(jobContext.Id, out var existingState))
        {
            // If the state exists and is different, update and notify
            if (existingState != newState)
            {
                currentStates[jobContext.Id] = newState;
                await NotifyStateChange(new JobStateMessage(jobContext, newState, status));
            }
        }
        else
        {
            // If the state does not exist, add it and notify as this is the first setting
            currentStates[jobContext.Id] = newState;
            await NotifyStateChange(new JobStateMessage(jobContext, newState, status));
        }
    }

    private async Task NotifyStateChange(JobStateMessage message)
    {
        await messageHub.PublishAsync(message);
        if (StateChangedAsync is not null)
        {
            foreach (var handler in StateChangedAsync.GetInvocationList().Cast<Func<JobStateMessage, Task>>())
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
