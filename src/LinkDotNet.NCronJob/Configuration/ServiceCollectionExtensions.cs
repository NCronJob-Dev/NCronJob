using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Extensions for the <see cref="IServiceCollection"/> to add cron jobs.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a job using an anonymous delegate to the service collection that gets executed based on the given cron expression.
    /// </summary>
    /// <param name="services">The service collection used to register the services.</param>
    /// <param name="jobDelegate">The delegate that represents the job to be executed.</param>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.</param>
    public static IServiceCollection AddJob(this IServiceCollection services, Delegate jobDelegate, string cronExpression)
    {
        services.AddNCronJob(builder =>
            builder.AddJob(jobDelegate, cronExpression)
        );

        return services;
    }
}




