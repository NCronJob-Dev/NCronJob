using Microsoft.Extensions.Configuration;

namespace LinkDotNet.NCronJob.Client;

internal static class Settings
{
    public static string JobSchedulerConfigurationSection = "NCronJob";

    public static IConfigurationSection GetSchedulerSection(this IConfiguration configuration, string name)
    {
        return configuration.GetSection(JobSchedulerConfigurationSection).GetSection(name);
    }

    public static IConfigurationSection GetSchedulerSection(this IConfiguration configuration)
    {
        return configuration.GetSection(JobSchedulerConfigurationSection);
    }
}

internal class HubConfiguration
{
    public string HubUrl { get; set; } = "http://localhost:7178/agent";
    public string? ApiKey { get; set; }

    public static HubConfiguration LoadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSchedulerSection("ControlCenter");
        var config = section?.Get<HubConfiguration>() ?? new HubConfiguration();
        return config;
    }
}
