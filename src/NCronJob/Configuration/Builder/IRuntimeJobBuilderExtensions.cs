using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Extensions for IRuntimeJobBuilder.
/// </summary>
public static class IRuntimeJobBuilderExtensions
{
    /// <summary>
    /// Adds a job to the service collection that gets executed based on the given cron expression.
    /// If a job with the same configuration is already registered, it will throw an exception.
    /// </summary>
    /// <param name="runtimeJobBuilder">The runtime job builder.</param>
    /// <param name="options">Configures the <see cref="JobOptionBuilder"/>, like the cron expression or parameters that get passed down.</param>
    public static void AddJob<TJob>(
        this IRuntimeJobBuilder runtimeJobBuilder,
        Action<JobOptionBuilder>? options = null)
        where TJob : class, IJob
    {
        ArgumentNullException.ThrowIfNull(runtimeJobBuilder);

        runtimeJobBuilder.AddJob(typeof(TJob), options);
    }
}
