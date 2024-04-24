using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCrontab;
using System.Reflection;
using System.Runtime;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents the builder for the NCronJob options.
/// </summary>
public sealed class NCronJobOptionBuilder
{
    private readonly IServiceCollection services;
    private readonly ConcurrencySettings settings;

    internal NCronJobOptionBuilder(IServiceCollection services, ConcurrencySettings settings)
    {
        this.services = services;
        this.settings = settings;
    }

    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="options">Configures the option, like the cron expression or parameters that get passed down.</param>
    /// <typeparam name="T">The job type. It will be registered scoped into the container.</typeparam>
    /// <exception cref="ArgumentException">Throws if the cron expression is invalid.</exception>
    /// <remarks>The cron expression is evaluated against UTC timezone.</remarks>
    /// <example>
    /// Registering a job that runs once every hour:
    /// <code>
    /// AddJob&lt;MyJob&gt;(c => c.WithCronExpression("0 * * * *").WithParameter("myParameter"));
    /// </code>
    /// </example>
    public NCronJobOptionBuilder AddJob<T>(Action<JobOptionBuilder>? options = null)
        where T : class, IJob
    {
        var builder = new JobOptionBuilder();
        options?.Invoke(builder);
        var jobOptions = builder.GetJobOptions();

        var concurrencyAttribute = typeof(T).GetCustomAttribute<SupportsConcurrencyAttribute>();
        if (concurrencyAttribute != null && concurrencyAttribute.MaxDegreeOfParallelism > settings.MaxDegreeOfParallelism)
        {
            throw new InvalidOperationException($"The MaxDegreeOfParallelism for {typeof(T).Name} " +
                                                $"({concurrencyAttribute.MaxDegreeOfParallelism}) cannot exceed " +
                                                $"the global limit ({settings.MaxDegreeOfParallelism}).");
        }

        foreach (var option in jobOptions.Where(c => !string.IsNullOrEmpty(c.CronExpression)))
        {
            var cron = GetCronExpression(option);
            var entry = new RegistryEntry(typeof(T), option.Parameter, cron);
            services.AddSingleton(entry);
        }

        services.TryAddScoped<T>();

        return this;
    }

    /// <summary>
    /// Adds a notification handler for a given <see cref="IJob"/>.
    /// </summary>
    /// <typeparam name="TJobNotificationHandler">The handler-type that is used to handle the job.</typeparam>
    /// <typeparam name="TJob">The job type.</typeparam>
    /// <remarks>
    /// The given <see cref="IJobNotificationHandler{TJob}"/> instance is registered as a scoped service sharing the same scope as the job.
    /// Also, only one handler per job is allowed. If multiple handlers are registered, only the first one will be executed.
    /// </remarks>
    public NCronJobOptionBuilder AddNotificationHandler<TJobNotificationHandler, TJob>()
        where TJobNotificationHandler : class, IJobNotificationHandler<TJob>
        where TJob : class, IJob
    {
        services.TryAddScoped<IJobNotificationHandler<TJob>, TJobNotificationHandler>();
        return this;
    }

    private static CrontabSchedule GetCronExpression(JobOption option)
    {
        var cronParseOptions = new CrontabSchedule.ParseOptions
        {
            IncludingSeconds = option.EnableSecondPrecision
        };

        return CrontabSchedule.TryParse(option.CronExpression, cronParseOptions)
               ?? throw new InvalidOperationException("Invalid cron expression");
    }
}
