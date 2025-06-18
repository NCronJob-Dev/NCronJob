namespace NCronJob;

/// <summary>
/// Extensions for DependencyBuilder.
/// </summary>
public static class DependencyBuilderExtensions
{
    /// <summary>
    /// Adds a job that runs after the principal job has finished with a given <paramref name="parameter"/>.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TJob"/> will automatically be registered in the container. There is no need to call <see cref="NCronJobOptionBuilder.AddJob{TJob}"/> for the dependent job.
    /// </remarks>
    public static DependencyBuilder RunJob<TJob>(this DependencyBuilder builder, object? parameter = null)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RunJob(typeof(TJob), parameter);
    }
}
