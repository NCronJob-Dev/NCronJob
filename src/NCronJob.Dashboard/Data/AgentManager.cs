using System.Collections.ObjectModel;
using System.Collections.Specialized;
using LinkDotNet.NCronJob.Shared;
using Microsoft.AspNetCore.SignalR;
using NCronJob.Dashboard.Hubs;

namespace NCronJob.Dashboard.Data
{
    internal class AgentManager
    {
        private record AgentDetails(string ConnectionId, IJobAgentCoordinator AgentProxy, ServiceAgent ServiceAgent);


        private readonly IHubContext<AgentHub, IJobAgentCoordinator> hubContext;
        private readonly Dictionary<string, AgentDetails> agentDetails = new();
        private readonly ObservableCollection<string> agents = new();

        public AgentManager(IHubContext<AgentHub, IJobAgentCoordinator> hubContext)
        {
            this.hubContext = hubContext;
            agents.CollectionChanged += OnAgentsCollectionChanged;
        }

        public IEnumerable<string> Agents => agents;

        public event NotifyCollectionChangedEventHandler StateChanged
        {
            add => agents.CollectionChanged += value;
            remove => agents.CollectionChanged -= value;
        }


        public void AddOrUpdateServiceAgent(string agentIdentifier, string connectionId, ServiceAgent serviceAgent)
        {
            lock (agentDetails)
            {
                var agentProxy = hubContext.Clients.Client(connectionId);
                if (agentDetails.ContainsKey(agentIdentifier))
                {
                    agentDetails[agentIdentifier] = new AgentDetails(connectionId, agentProxy, serviceAgent);
                }
                else
                {
                    agentDetails.Add(agentIdentifier, new AgentDetails(connectionId, agentProxy, serviceAgent));
                    agents.Add(agentIdentifier); // Update observable collection for UI
                }
            }
        }
        

        public void RemoveAgent(string agentIdentifier)
        {
            lock (agentDetails)
            {
                if (agentDetails.Remove(agentIdentifier))
                {
                    agents.Remove(agentIdentifier); // Keep observable collection in sync
                }
            }
        }

        public void RemoveAgentByConnectionId(string connectionId)
        {
            lock (agentDetails)
            {
                var agentIdentifier = agentDetails.FirstOrDefault(kv => kv.Value.ConnectionId == connectionId).Key;
                if (agentIdentifier != null)
                {
                    agentDetails.Remove(agentIdentifier);
                    agents.Remove(agentIdentifier); // Ensure synchronization with the observable collection
                }
            }
        }

        public IJobAgentCoordinator GetAgentProxy(string agentIdentifier)
        {
            lock (agentDetails)
            {
                if (agentDetails.TryGetValue(agentIdentifier, out var details))
                {
                    return details.AgentProxy;
                }
                else
                {
                    throw new InvalidOperationException($"Agent with identifier {agentIdentifier} not found.");
                }
            }
        }

        public ServiceAgent GetServiceAgentByAgentIdentifier(string agentIdentifier)
        {
            lock (agentDetails)
            {
                if (agentDetails.TryGetValue(agentIdentifier, out var details))
                {
                    return details.ServiceAgent;
                }
                else
                {
                    throw new InvalidOperationException($"ServiceAgent with identifier {agentIdentifier} not found.");
                }
            }
        }

        public string? GetAgentIdentifierByConnectionId(string connectionId)
        {
            lock (agentDetails)
            {
                return agentDetails.FirstOrDefault(kv => kv.Value.ConnectionId == connectionId).Key;
            }
        }

        public bool IsAgentActive(string agentIdentifier)
        {
            lock (agents)
            {
                return agents.Contains(agentIdentifier);
            }
        }

        private void OnAgentsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // This method can be used to react to changes in the collection of agents.
        }
    }


}
