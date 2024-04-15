using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Extensions for the <see cref="IServiceCollection"/> to add cron jobs.
/// </summary>
public static class NCronJobExtensions
{
    /// <summary>
    /// Adds NCronJob services to the service container.
    /// </summary>
    /// <param name="services">The service collection used to register the services.</param>
    /// <param name="options">The builder to register jobs and other settings.</param>
    public static IServiceCollection AddNCronJob(
        this IServiceCollection services,
        Action<NCronJobOptionBuilder>? options = null)
    {
        var builder = new NCronJobOptionBuilder(services);
        options?.Invoke(builder);

        services.AddHostedService<CronScheduler>();
        services.AddSingleton<CronRegistry>();
        services.AddSingleton<JobExecutor>();
        services.AddSingleton<IInstantJobRegistry>(c => c.GetRequiredService<CronRegistry>());
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
