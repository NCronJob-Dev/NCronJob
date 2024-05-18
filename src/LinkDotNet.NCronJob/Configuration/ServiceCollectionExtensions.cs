using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.NCronJob;

/// <summary>
/// Extensions for the <see cref="IServiceCollection"/> to add cron jobs.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a job using an anonymous delegate to the service collection that gets executed based on the given cron expression.
    /// This method allows for the scheduling of either synchronous or asynchronous tasks which are defined using lambda expressions.
    /// The delegate can depend on services registered in the dependency injection container, which are resolved at runtime.
    /// </summary>
    /// <param name="services">The service collection used to register the services.</param>
    /// <param name="jobDelegate">The delegate that represents the job to be executed. This delegate must return either void or Task.
    ///     <example>
    ///         Synchronous job example:
    ///         <code>
    ///             builder.Services.AddJob((ILogger&lt;Program&gt; logger, TimeProvider timeProvider) =&gt;
    ///             {
    ///                 logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
    ///             }, "*/40 * * * * *");
    ///         </code>
    ///         Asynchronous job example:
    ///         <code>
    ///             builder.Services.AddJob(async (ILogger&lt;Program&gt; logger, TimeProvider timeProvider, CancellationToken ct) =&gt;
    ///             {
    ///                 logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
    ///                 await Task.Delay(1000, ct);
    ///             }, "*/40 * * * * *");
    ///         </code>
    ///         Synchronous job with retry policy example:
    ///         <code>
    ///             builder.Services.AddJob([RetryPolicy(retryCount: 4)] (JobExecutionContext context, ILogger&lt;Program&gt; logger) =&gt;
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
    ///     </example>
    /// </param>
    /// <param name="cronExpression">The cron expression that defines when the job should be executed.
    ///     <example>
    ///         Example of cron expression: "*/5 * * * * *"
    ///         This expression schedules the job to run every 5 seconds.
    ///     </example>
    /// </param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddJob(this IServiceCollection services, Delegate jobDelegate, string cronExpression)
    {
        services.AddNCronJob(builder =>
            builder.AddJob(jobDelegate, cronExpression)
        );

        return services;
    }
}




