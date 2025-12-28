using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCronJob.Dashboard.Services;

namespace NCronJob.Dashboard;

/// <summary>
/// Extension methods for adding and configuring the NCronJob Dashboard.
/// </summary>
public static class NCronJobDashboardExtensions
{
    /// <summary>
    /// Adds the NCronJob Dashboard services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddNCronJobDashboard(
        this IServiceCollection services,
        Action<NCronJobDashboardOptions>? configure = null)
    {
        var options = new NCronJobDashboardOptions();
        configure?.Invoke(options);
        
        services.TryAddSingleton(options);
        services.TryAddSingleton<DashboardService>();
        
        // Add Razor Components with Interactive Server support
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        // Add antiforgery services
        services.AddAntiforgery();
        
        return services;
    }

    /// <summary>
    /// Maps the NCronJob Dashboard to the specified endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the dashboard. Defaults to "/ncronjob-dashboard".</param>
    /// <returns>The endpoint route builder for method chaining.</returns>
    public static IEndpointRouteBuilder UseNCronJobDashboard(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ncronjob-dashboard")
    {
        endpoints.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        return endpoints;
    }
}

/// <summary>
/// Options for configuring the NCronJob Dashboard.
/// </summary>
public class NCronJobDashboardOptions
{
    /// <summary>
    /// Gets or sets the title displayed in the dashboard.
    /// </summary>
    public string Title { get; set; } = "NCronJob Dashboard";

    /// <summary>
    /// Gets or sets whether to show the job parameter details.
    /// </summary>
    public bool ShowJobParameters { get; set; } = true;

    /// <summary>
    /// Gets or sets the refresh interval in milliseconds for real-time updates.
    /// </summary>
    public int RefreshIntervalMs { get; set; } = 1000;
}
