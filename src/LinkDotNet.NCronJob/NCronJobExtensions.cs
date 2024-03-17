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
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="services">The service collection used to register the job.</param>
    /// <param name="options">Configures the option, like the cron expression or parameters that get passed down.</param>
    /// <typeparam name="T">The job type.</typeparam>
    /// <exception cref="ArgumentException">Throws if the cron expression is invalid.</exception>
    /// <remarks>The cron expression is evaluated against UTC timezone.</remarks>
    public static IServiceCollection AddCronJob<T>(this IServiceCollection services, Action<JobOption>? options = null)
        where T : class, IJob
    {
        JobOption option = new();
        options?.Invoke(option);

        if (!string.IsNullOrEmpty(option.CronExpression))
        {
            var cron = CrontabSchedule.TryParse(option.CronExpression)
                       ?? throw new InvalidOperationException("Invalid cron expression");
            var entry = new CronRegistryEntry(typeof(T), new(option.Parameter), cron);
            services.AddSingleton(entry);
        }

        services.AddHostedService<CronScheduler>();
        services.TryAddSingleton<CronRegistry>();
        services.TryAddSingleton<IInstantJobRegistry, CronRegistry>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<T>();

        return services;
    }
}
