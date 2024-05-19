using System.Threading.Channels;
using LinkDotNet.NCronJob.Shared;
using Microsoft.AspNetCore.SignalR;
using NCronJob.Dashboard.Data;

namespace NCronJob.Dashboard.Hubs;

internal class AgentHub(
    AgentManager agentManager,
    NotificationService notificationService)
    : Hub<IJobAgentCoordinator>
{

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"User {Context.UserIdentifier} connected");
        return base.OnConnectedAsync();
    }


    public override Task OnDisconnectedAsync(Exception? exception)
    {
        agentManager.RemoveAgentByConnectionId(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
    
    public async Task RegisterServiceAgent(ServiceAgent agent)
    {
        Console.WriteLine($"Service Agent {agent.ServiceId} registered!");

        // Use agent.ServiceID as the unique identifier for the agent
        agentManager.AddOrUpdateServiceAgent(agent.ServiceId, Context.ConnectionId, agent);

        await Groups.AddToGroupAsync(Context.ConnectionId, agent.ServiceId);
    }

    public Task ReceiveJobDetailsInformationBatch(List<JobDetailsView> jobDetails)
    {
        if (IsAgentInvocationAllowed())
        {
            // Group by JobName and select the most recent JobDetailsView for each JobName
            var mostRecentJobDetails = jobDetails
                .GroupBy(j => j.JobName)
                .Select(g => g.Last()) // Assumes jobDetails are added in chronological order
                .ToList();

            var agentIdentifier = agentManager.GetAgentIdentifierByConnectionId(Context.ConnectionId);
            foreach (var jobDetail in mostRecentJobDetails)
            {
                notificationService.NotifyJobDetailsReceivedForAgent(jobDetail, agentIdentifier!);
            }
        }
        return Task.CompletedTask;
    }


    private bool IsAgentInvocationAllowed()
    {
        var agentIdentifier = agentManager.GetAgentIdentifierByConnectionId(Context.ConnectionId);
        return !string.IsNullOrEmpty(agentIdentifier) && agentManager.IsAgentActive(agentIdentifier);
    }
}
