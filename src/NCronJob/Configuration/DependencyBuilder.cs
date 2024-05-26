namespace NCronJob;

/// <summary>
/// Represents the builder for the dependent jobs.
/// </summary>
public sealed class DependencyBuilder<TPrincipalJob>
    where TPrincipalJob : IJob
{
    private readonly List<JobDefinition> dependentJobOptions = [];

    /// <summary>
    /// Adds a job that runs after the principal job has finished with a given <paramref name="parameter"/>.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TJob"/> will automatically be registered in the container. There is no need to call <see cref="NCronJobOptionBuilder.AddJob{TJob}"/> for the dependent job.
    /// </remarks>
    public DependencyBuilder<TPrincipalJob> RunJob<TJob>(object? parameter = null)
        where TJob : IJob
    {
        dependentJobOptions.Add(new JobDefinition(typeof(TJob), parameter, null, null));
        return this;
    }

    internal List<JobDefinition> GetDependentJobOption() => dependentJobOptions;
}
