using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Extensions for the <see cref="IServiceCollection"/> to add cron jobs.
/// </summary>
public static class ServiceCollectionExtensions
{
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
}




