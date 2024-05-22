using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCronJob;

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
    /// <param name="configureGlobalOptions">Allows configuration of global behavior across all jobs.
    /// If multiple instances of <see cref="NCronJobExtensions.AddNCronJob"/> are called with different global settings,
    /// only the first one will be taken into account.</param>
    /// <example>
    /// To register a job that runs once every hour with a parameter and a handler that gets notified once the job is completed:
    /// <code>
    /// Services.AddNCronJob(options =>
    ///  .AddJob&lt;MyJob&gt;(c => c.WithCronExpression("0 * * * *").WithParameter("myParameter"))
    ///  .AddNotificationHandler&lt;MyJobHandler, MyJob&gt;());
    /// </code>
    /// </example>
    public static IServiceCollection AddNCronJob(
        this IServiceCollection services,
        Action<NCronJobOptionBuilder>? options = null,
        Action<GlobalOptions>? configureGlobalOptions = null)
    {
        var globalOptions = new GlobalOptions();
        configureGlobalOptions?.Invoke(globalOptions);
        var builder = new NCronJobOptionBuilder(services, globalOptions.ToConcurrencySettings());
        options?.Invoke(builder);

        services.TryAddSingleton(globalOptions.ToConcurrencySettings());
        services.AddHostedService<QueueWorker>();
        services.TryAddSingleton<JobRegistry>();
        services.TryAddSingleton<JobQueue>();
        services.TryAddSingleton<JobExecutor>();
        services.TryAddSingleton<IRetryHandler, RetryHandler>();
        services.TryAddSingleton<IInstantJobRegistry, InstantJobRegistry>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
