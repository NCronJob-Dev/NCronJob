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
        var jobRegistry = services.FirstOrDefault(d => d.ServiceType == typeof(JobRegistry))?.ImplementationInstance as JobRegistry
                            ?? new JobRegistry();

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
        services.TryAddSingleton<JobExecutionProgressObserver>();
        services.TryAddSingleton<IJobExecutionProgressReporter, JobExecutionProgressObserver>((sp) =>
        {
            return sp.GetRequiredService<JobExecutionProgressObserver>();
        });
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<StartupJobManager>();
        services.TryAddSingleton<MissingMethodCalledHandler>();

        return services;
    }

    /// <summary>
    /// Adds a job using an anonymous delegate to the service collection that gets executed based on the given cron expression.
    /// This method allows for the scheduling of either synchronous or asynchronous tasks which are defined using lambda expressions.
    /// The delegate can depend on services registered in the dependency injection container, which are resolved at runtime.
    /// </summary>
    /// <param name="services">The service collection used to register the services.</param>
    /// <param name="jobDelegate">The delegate that represents the job to be executed. This delegate must return either void or Task.</param>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.
    ///     <example>
    ///         Example of cron expression: "*/5 * * * * *"
    ///         This expression schedules the job to run every 5 seconds.
    ///     </example>
    /// </param>
    /// <param name="timeZoneInfo">The time zone information that the cron expression should be evaluated against.
    /// If not set the default time zone is UTC.
    /// </param>
    ///     <example>
    ///         Synchronous job example:
    ///         <code>
    ///             builder.Services.AddNCronJob((ILogger&lt;Program&gt; logger, TimeProvider timeProvider) =&gt;
    ///             {
    ///                 logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
    ///             }, "*/40 * * * * *");
    ///         </code>
    ///         Asynchronous job example:
    ///         <code>
    ///             builder.Services.AddNCronJob(async (ILogger&lt;Program&gt; logger, TimeProvider timeProvider, CancellationToken ct) =&gt;
    ///             {
    ///                 logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
    ///                 await Task.Delay(1000, ct);
    ///             }, "*/40 * * * * *");
    ///         </code>
    ///         Synchronous job with retry policy example:
    ///         <code>
    ///             builder.Services.AddNCronJob([RetryPolicy(retryCount: 4)] (JobExecutionContext context, ILogger&lt;Program&gt; logger) =&gt;
    ///             {
    ///                 var attemptCount = context.Attempts;
    ///                 if (attemptCount &lt;= 4)
    ///                 {
    ///                     logger.LogWarning("TestRetryJob simulating failure.");
    ///                     throw new InvalidOperationException("Simulated operation failure in TestRetryJob.");
    ///                 }
    ///                 logger.LogInformation($"Job ran after {attemptCount} attempts");
    ///             }, "*/5 * * * * *");
    ///         </code>
    ///         Synchronous job example with TimeZone:
    ///         <code>
    ///             builder.Services.AddNCronJob((ILogger&lt;Program&gt; logger, TimeProvider timeProvider) =&gt;
    ///             {
    ///                 logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
    ///             }, "*/40 * * * * *", TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
    ///         </code>
    ///     </example>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddNCronJob(this IServiceCollection services, Delegate jobDelegate, string cronExpression, TimeZoneInfo? timeZoneInfo = null)
        => services.AddNCronJob(builder => builder.AddJob(jobDelegate, cronExpression, timeZoneInfo));

    /// <summary>
    /// Configures the host to use NCronJob. This will also start any given startup jobs and their dependencies.
    /// </summary>
    /// <remarks>
    /// Failure to call this method (or <see cref="UseNCronJobAsync(IHost)"/>) when startup jobs are defined will lead to a fatal exception during the application start.
    /// </remarks>
    /// <param name="host">The host.</param>
    public static IHost UseNCronJob(this IHost host) => UseNCronJobAsync(host).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Configures the host to use NCronJob. This will also start any given startup jobs and their dependencies.
    /// </summary>
    /// <remarks>
    /// Failure to call this method (or <see cref="UseNCronJob(IHost)"/>) when startup jobs are defined will lead to a fatal exception during the application start.
    /// </remarks>
    /// <param name="host">The host.</param>
    public static async Task<IHost> UseNCronJobAsync(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var jobManager = host.Services.GetRequiredService<StartupJobManager>();
        var stopToken = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        await jobManager.ProcessStartupJobs(stopToken);

        host.Services.GetRequiredService<MissingMethodCalledHandler>().UseWasCalled = true;

        return host;
    }

    // Inspired by https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.DependencyInjection.Abstractions/src/Extensions/ServiceCollectionDescriptorExtensions.cs
    // License MIT
    private static IServiceCollection TryAddSingleton<TService, TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, TImplementation> implementationFactory)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationFactory);

        var descriptor = ServiceDescriptor.Singleton<TService, TImplementation>(implementationFactory);

        services.TryAdd(descriptor);

        return services;
    }
}
