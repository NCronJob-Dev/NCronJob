namespace NCronJob;

/// <summary>
/// Represents the builder for the dependent jobs.
/// </summary>
public sealed class DependencyBuilder
{
    private readonly List<JobDefinition> dependentJobOptions = [];

    /// <summary>
    /// Adds a job that runs after the principal job has finished with a given <paramref name="parameter"/>.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TJob"/> will automatically be registered in the container. There is no need to call <see cref="NCronJobOptionBuilder.AddJob{TJob}"/> for the dependent job.
    /// </remarks>
    public DependencyBuilder RunJob<TJob>(object? parameter = null)
        where TJob : IJob
    {
        dependentJobOptions.Add(JobDefinition.CreateTyped(typeof(TJob), parameter));
        return this;
    }

    /// <summary>
    /// Adds an anonymous delegate job that runs after the principal job has finished.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed. This delegate must return either void or Task.</param>
    /// <param name="jobName">Sets the job name that can be used to identify and manipulate the job later on.</param>
    public DependencyBuilder RunJob(Delegate jobDelegate, string? jobName = null)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);

        var jobDefinition = JobDefinition.CreateUntyped(jobName, jobDelegate);
        dependentJobOptions.Add(jobDefinition);
        return this;
    }

    internal List<JobDefinition> GetDependentJobOption() => dependentJobOptions;
}
