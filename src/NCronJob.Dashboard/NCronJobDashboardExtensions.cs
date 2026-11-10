using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCronJob.Dashboard.Components;

namespace NCronJob.Dashboard;

/// <summary>
/// Extensions to host the NCronJob dashboard.
/// </summary>
public static class NCronJobDashboardExtensions
{
    // WebApplication shares its endpoint route builder with every branch; removing it keeps the dashboard endpoints inside its branch.
    private const string GlobalEndpointRouteBuilderKey = "__GlobalEndpointRouteBuilder";

    /// <summary>
    /// Registers the services needed by the NCronJob dashboard.
    /// </summary>
    public static IServiceCollection AddNCronJobDashboard(this IServiceCollection services)
    {
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddAuthorization();
        services.TryAddSingleton<DashboardRunStore>();
        services.Replace(ServiceDescriptor.Singleton(RecordedObserver));
        return services;
    }

    // Startup jobs can run before the dashboard is mapped, so the store subscribes as soon as the observer exists.
    private static JobExecutionProgressObserver RecordedObserver(IServiceProvider services)
    {
        var observer = ActivatorUtilities.CreateInstance<JobExecutionProgressObserver>(services);
        services.GetRequiredService<DashboardRunStore>().Subscribe(observer);
        return observer;
    }

    /// <summary>
    /// Serves the NCronJob dashboard under <paramref name="path"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="path">The path the dashboard is served from.</param>
    /// <param name="configureEndpoints">
    /// Configures the dashboard endpoints (pages and the Blazor hub), for example
    /// <c>e => e.RequireAuthorization("Admin")</c>.
    /// </param>
    public static IApplicationBuilder MapNCronJobDashboard(
        this IApplicationBuilder app,
        string path = "/ncronjob",
        Action<IEndpointConventionBuilder>? configureEndpoints = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Map(path, branch =>
        {
            branch.Properties.Remove(GlobalEndpointRouteBuilderKey);
            branch.Use(EmbeddedAssets.ServeAsync);
            branch.Use((context, next) =>
            {
                context.SetEndpoint(null);
                context.Request.RouteValues.Clear();
                return next(context);
            });
            branch.UseRouting();
            branch.UseAuthorization();
            branch.UseAntiforgery();
            branch.UseEndpoints(endpoints =>
            {
                var dashboard = endpoints.MapRazorComponents<DashboardApp>().AddInteractiveServerRenderMode();
                configureEndpoints?.Invoke(dashboard);
            });
        });
    }
}
