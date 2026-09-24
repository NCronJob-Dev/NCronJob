namespace NCronJob;

/// <summary>
/// Defines the contract for a stage in the job lifecycle where the job is set to run at startup.
/// </summary>
/// <typeparam name="TJob">The type of the job to be run at startup.</typeparam>
public interface IStartupStage<TJob> : INotificationStage<TJob>
    where TJob : class, IJob
{
    /// <summary>
    /// Configures the job to run once before the application itself runs.
    /// </summary>
    /// <param name="shouldCrashOnFailure">When <code>true</code>, will lead to a fatal exception during the application start would the job crash. Default is <code>false</code>.</param>
    /// <returns>Returns a <see cref="INotificationStage{TJob}"/> that allows adding notifications of another job.</returns>
    /// <remarks>
    /// If a job is marked to run at startup, it will be executed before any `IHostedService` is started. Use the <seealso cref="NCronJobExtensions.UseNCronJob"/> method to trigger the job execution.
    /// In the context of ASP.NET:
    /// <code>
    /// await app.UseNCronJobAsync();
    /// await app.RunAsync();
    /// </code>
    /// All startup jobs will be executed (and awaited) before the web application is started. This is particular useful for migration and cache hydration.
    /// </remarks>
    [Obsolete("This method will be dropped in the next major version. Configure startup jobs through the JobOptionBuilder fluent interface instead.")]
    INotificationStage<TJob> RunAtStartup(bool shouldCrashOnFailure = false);
}
