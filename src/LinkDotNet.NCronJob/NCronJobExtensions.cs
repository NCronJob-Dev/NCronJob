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
        Action<NCronJobOptionBuilder>? options = null)
    {
        // 4 is just an arbitrary multiplier based on system observed I/O, this could come from Configuration
        var settings = new ConcurrencySettings { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
        var builder = new NCronJobOptionBuilder(services, settings);
        options?.Invoke(builder);

        if (services.IsServiceRegistered<JobRegistry>())
            return services;

        services.AddSingleton(settings);
        services.AddHostedService<QueueWorker>();
        services.AddSingleton<JobRegistry>();
        services.AddSingleton<JobQueue>();
        services.AddSingleton<JobExecutor>();
        services.TryAddSingleton<IRetryHandler, RetryHandler>();
        services.AddSingleton<IInstantJobRegistry, InstantJobRegistry>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    private static bool IsServiceRegistered<TService>(this IServiceCollection services) =>
        services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService));
}
