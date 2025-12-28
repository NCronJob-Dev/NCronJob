namespace NCronJob;

/// <summary>
/// Represents the builder for the dependent jobs.
/// </summary>
public sealed class DependencyBuilder
{
    private readonly List<JobDefinition> dependentJobOptions = [];
    private DependentJobBuilder? lastBuilder;

    /// <summary>
    /// Adds a job that runs after the principal job has finished with a given <paramref name="parameter"/>.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TJob"/> will automatically be registered in the container. There is no need to call <see cref="NCronJobOptionBuilder.AddJob{TJob}"/> for the dependent job.
    /// </remarks>
    public DependentJobBuilder RunJob<TJob>(object? parameter = null)
        where TJob : IJob
    {
        // Apply any pending job options from the last builder
        lastBuilder?.ApplyJobOption();
        
        var jobDefinition = JobDefinition.CreateTyped(typeof(TJob), parameter);
        dependentJobOptions.Add(jobDefinition);
        lastBuilder = new DependentJobBuilder(this, jobDefinition);
        return lastBuilder;
    }

    /// <summary>
    /// Adds an anonymous delegate job that runs after the principal job has finished.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed. This delegate must return either void or Task.</param>
    /// <param name="jobName">Sets the job name that can be used to identify and manipulate the job later on.</param>
    public DependentJobBuilder RunJob(Delegate jobDelegate, string? jobName = null)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);

        // Apply any pending job options from the last builder
        lastBuilder?.ApplyJobOption();

        var jobDefinition = JobDefinition.CreateUntyped(jobName, jobDelegate);
        dependentJobOptions.Add(jobDefinition);
        lastBuilder = new DependentJobBuilder(this, jobDefinition);
        return lastBuilder;
    }

    internal List<JobDefinition> GetDependentJobOption()
    {
        // Apply any pending job options from the last builder before returning
        lastBuilder?.ApplyJobOption();
        return dependentJobOptions;
    }
}
