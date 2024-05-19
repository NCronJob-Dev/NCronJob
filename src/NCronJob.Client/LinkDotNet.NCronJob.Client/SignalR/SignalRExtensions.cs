using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob.Client;

internal static class SignalRExtensions
{
    public static IServiceCollection AddSignalRHubConnection(this IServiceCollection services, HubConfiguration hubConfig)
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(hubConfig.HubUrl, o =>
            {
                o.SkipNegotiation = true;
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                o.AccessTokenProvider = () => Task.FromResult(hubConfig.ApiKey);
            })
            .WithStatefulReconnect()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
                logging.AddFilter("Microsoft.AspNetCore.Http.Connections.Client.HttpConnection", LogLevel.Critical);
                logging.AddConsole();
            })
            .WithAutomaticReconnect(new RetryPolicyLoop())
            .WithServerTimeout(TimeSpan.FromSeconds(60))
            .WithKeepAliveInterval(TimeSpan.FromSeconds(30))
            .Build();

        // Configure HubConnection options globally
        services.Configure<HubConnectionOptions>(o => o.StatefulReconnectBufferSize = 200_000);

        services.AddLazyResolution();

        services.AddSingleton<HubConnection>(hubConnection);

        return services;
    }
}

public static class ServiceCollectionExtensions
{
    public static void AddLazyResolution(this IServiceCollection services)
    {
        services.AddTransient(typeof(Lazy<>), typeof(LazilyResolved<>));
    }

    private class LazilyResolved<T>(IServiceProvider serviceProvider)
        : Lazy<T>(serviceProvider.GetRequiredService<T>)
        where T : notnull;
}
