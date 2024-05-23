using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace NCronJob;

/// <summary>
/// Extensions for the <see cref="IServiceCollection"/> to add cron jobs.
/// </summary>
public static class NCronJobExtensions
{
    private static Lazy<GlobalOptions> globalOptions = new(() => new GlobalOptions());

    /// <summary>
    /// Used to reset the global options to the default values to use for Unit Testing.
    /// </summary>
    internal static void ResetGlobalOptions() => globalOptions = new Lazy<GlobalOptions>(() => new GlobalOptions());

    /// <summary>
    /// Adds NCronJob services to the service container.
    /// </summary>
    /// <param name="services">The service collection used to register the services.</param>
    /// <param name="options">The builder to register jobs and other settings.</param>
    /// <param name="configureGlobalOptions">Allows configuration of global behavior across all jobs.
    /// If multiple instances of "NCronJobExtensions.AddNCronJob" are called with different global settings,
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
        ConfigureGlobalOptions(configureGlobalOptions);

        var builderOptions = new NCronJobOptionBuilder(services, globalOptions.Value.ToConcurrencySettings());
        options?.Invoke(builderOptions);

        RegisterServices(services);

        return services;
    }

    /// <summary>
    /// Adds NCronJob services to the web application builder.
    /// </summary>
    /// <param name="builder">The web application builder used to register the services.</param>
    /// <param name="options">The builder to register jobs and other settings.</param>
    /// <param name="configureGlobalOptions">Allows configuration of global behavior across all jobs.
    /// If multiple instances of "AddNCronJob" are called with different global settings,
    /// only the first one will be taken into account. Subsequent calls will log a warning and use the initial settings.</param>
    /// <returns>The updated <see cref="WebApplicationBuilder"/>.</returns>
    /// <example>
    /// To register a job that runs once every hour with a parameter and a handler that gets notified once the job is completed:
    /// <code>
    /// builder.AddNCronJob(options =>
    ///  .AddJob&lt;MyJob&gt;(c => c.WithCronExpression("0 * * * *").WithParameter("myParameter"))
    ///  .AddNotificationHandler&lt;MyJobHandler, MyJob&gt;());
    /// </code>
    /// </example>
    public static WebApplicationBuilder AddNCronJob(this WebApplicationBuilder builder,
        Action<NCronJobOptionBuilder>? options = null,
        Action<GlobalOptions>? configureGlobalOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        var configuration = builder.Configuration;

        ConfigureGlobalOptions(configureGlobalOptions, configuration);

        var builderOptions = new NCronJobOptionBuilder(services, globalOptions.Value.ToConcurrencySettings());
        options?.Invoke(builderOptions);

        RegisterServices(services);

        return builder;
    }

    private static void ConfigureGlobalOptions(
        Action<GlobalOptions>? configureGlobalOptions,
        ConfigurationManager? configuration = null)
    {
        if (globalOptions.IsValueCreated)
        {
            LogGlobalOptionsWarning();
        }
        else
        {
            var globalOptionsValue = globalOptions.Value;
            if (configuration != null)
            {
                var configSection = configuration.GetSection("NCronJob:GlobalOptions");

                if (configSection.Exists())
                {
                    configSection.Bind(globalOptionsValue);
                }
            }

            configureGlobalOptions?.Invoke(globalOptionsValue);
        }
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.TryAddSingleton(globalOptions.Value.ToConcurrencySettings());
        services.AddHostedService<QueueWorker>();
        services.TryAddSingleton<JobRegistry>();
        services.TryAddSingleton<JobQueue>();
        services.TryAddSingleton<JobExecutor>();
        services.TryAddSingleton<IRetryHandler, RetryHandler>();
        services.TryAddSingleton<IInstantJobRegistry, InstantJobRegistry>();
        services.TryAddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// Logs a warning about global options already being set.
    /// We must create a logger factory because ILogger is not available
    /// before building the WebApplication host.
    /// </summary>
    private static void LogGlobalOptionsWarning()
    {
        using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
            .SetMinimumLevel(LogLevel.Trace)
            .AddConsole());

        ILogger logger = loggerFactory.CreateLogger<GlobalOptions>();
        GlobalOptionsWarning(logger, null);
    }

    private static readonly Action<ILogger, Exception?> GlobalOptionsWarning = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(0, "GlobalOptionsWarning"),
        "Warning: GlobalOptions have already been set. Using existing values and ignoring new configuration.");
}
