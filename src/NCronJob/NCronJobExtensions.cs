using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace NCronJob;

/// <summary>
/// Extensions for various types to use NCronJob.
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
        JobRegistry jobRegistry = new();

        // 4 is just an arbitrary multiplier based on system observed I/O, this could come from Configuration
        var settings = new ConcurrencySettings { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };

        var builder = new NCronJobOptionBuilder(services, settings, jobRegistry);
        options?.Invoke(builder);

        services.TryAddSingleton(settings);
        services.AddHostedService<QueueWorker>();
        services.TryAddSingleton(jobRegistry);
        services.TryAddSingleton<JobQueueManager>();
        services.TryAddSingleton<JobWorker>();
        services.TryAddSingleton<JobProcessor>();
        services.TryAddSingleton<JobExecutor>();
        services.TryAddSingleton<IRetryHandler, RetryHandler>();
        services.TryAddSingleton<IInstantJobRegistry, InstantJobRegistry>();
        services.TryAddSingleton<IRuntimeJobRegistry, RuntimeJobRegistry>(sp => new RuntimeJobRegistry(
            services,
            jobRegistry,
            sp.GetRequiredService<JobWorker>(),
            sp.GetRequiredService<JobQueueManager>(),
            sp.GetRequiredService<ConcurrencySettings>()));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<StartupJobManager>();

        return services;
    }

    /// <summary>
    /// Configures the host to use NCronJob. This will also start any given startup jobs and their dependencies.
    /// </summary>
    /// <param name="host">The host.</param>
    public static IHost UseNCronJob(this IHost host) => UseNCronJobAsync(host).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Configures the host to use NCronJob. This will also start any given startup jobs and their dependencies.
    /// </summary>
    /// <param name="host">The host.</param>
    public static async Task<IHost> UseNCronJobAsync(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var jobManager = host.Services.GetRequiredService<StartupJobManager>();
        var stopToken = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        await jobManager.ProcessStartupJobs(stopToken);

        return host;
    }
}
