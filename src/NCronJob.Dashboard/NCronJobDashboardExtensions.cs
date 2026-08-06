using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCronJob.Dashboard;

namespace NCronJob;

/// <summary>Registration extensions for the NCronJob dashboard.</summary>
public static class NCronJobDashboardExtensions
{
    /// <summary>Adds the dashboard data store and embedded web host.</summary>
    public static IServiceCollection AddNCronJobDashboard(
        this IServiceCollection services,
        Action<NCronJobDashboardOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services
            .FirstOrDefault(service => service.ServiceType == typeof(NCronJobDashboardOptions))
            ?.ImplementationInstance as NCronJobDashboardOptions ?? new NCronJobDashboardOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<DashboardDataStore>();
        services.TryAddSingleton<DashboardControls>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<DashboardDataStore>());
        services.AddHostedService<DashboardWebHostService>();
        return services;
    }

    /// <summary>Applies final dashboard options after the host has been built.</summary>
    public static IHost UseNCronJobDashboard(
        this IHost host,
        Action<NCronJobDashboardOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        configure?.Invoke(host.Services.GetRequiredService<NCronJobDashboardOptions>());
        return host;
    }
}
