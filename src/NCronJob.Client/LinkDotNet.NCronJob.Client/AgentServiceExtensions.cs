using LinkDotNet.NCronJob.Shared;
using LinkDotNet.NCronJob.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkDotNet.NCronJob.Client;

public static class AgentServiceExtensions
{
    public static IServiceCollection AddCronJobControlCenterServices(this IServiceCollection services, IConfiguration configuration)
    {
        var hubConfig = HubConfiguration.LoadFromConfiguration(configuration);
        services.AddSingleton(hubConfig);
        if (!string.IsNullOrEmpty(hubConfig.HubUrl))
        {
            services.AddSignalRHubConnection(hubConfig);
        }
        
        services.AddSingleton<IJobAgentCoordinator, JobAgentCoordinator>();
        services.Configure<AgentHubClientOptions>(configuration.GetSchedulerSection("AgentHubClient"));

        services.TryAddTransient<IJobService, JobService>();
        services.AddHostedService<AgentHubClient>();

        services.TryAddSingleton<BufferedSignalRNotifier>();
        
        return services;
    }
}
