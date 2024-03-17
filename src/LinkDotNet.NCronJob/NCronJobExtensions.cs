using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCrontab;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Extensions for the <see cref="IServiceCollection"/> to add cron jobs.
/// </summary>
public static class NCronJobExtensions
{
    /// <summary>
    /// Adds a job definition to the service collection that can get executed instantly.
    /// </summary>
    /// <param name="services">The service collection used to register the job.</param>
    /// <typeparam name="T">The job type.</typeparam>
    public static IServiceCollection AddJob<T>(this IServiceCollection services)
        where T : class, IJob
    {
        services.AddCommonServices();
        services.TryAddSingleton<T>();

        return services;
    }

    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="services">The service collection used to register the job.</param>
    /// <param name="cronExpression">The cron expression on which the job gets executed.</param>
    /// <typeparam name="T">The job type.</typeparam>
    /// <exception cref="ArgumentException">Throws if the cron expression is invalid.</exception>
    public static IServiceCollection AddCronJob<T>(this IServiceCollection services, string cronExpression)
        where T : class, IJob
    {
        var cron = CrontabSchedule.TryParse(cronExpression)
                   ?? throw new ArgumentException("Invalid cron expression", nameof(cronExpression));

        var entry = new CronRegistryEntry(typeof(T), cron);

        services.AddCommonServices();
        services.TryAddSingleton<T>();
        services.AddSingleton(entry);

        return services;
    }

    private static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        services.AddHostedService<CronScheduler>();
        services.TryAddSingleton<CronRegistry>();
        services.TryAddSingleton<IInstantJobRegistry, CronRegistry>();

        return services;
    }
}
