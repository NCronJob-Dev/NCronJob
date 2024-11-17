namespace NCronJob;

/// <summary>
/// Represents the builder for the dependent jobs.
/// </summary>
public sealed class DependencyBuilder<TPrincipalJob>
    where TPrincipalJob : IJob
{
    private readonly List<JobDefinition> dependentJobOptions = [];
    private readonly JobRegistry jobRegistry;

    internal DependencyBuilder(JobRegistry jobRegistry) => this.jobRegistry = jobRegistry;

    /// <summary>
    /// Adds a job that runs after the principal job has finished with a given <paramref name="parameter"/>.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TJob"/> will automatically be registered in the container. There is no need to call <see cref="NCronJobOptionBuilder.AddJob{TJob}"/> for the dependent job.
    /// </remarks>
    public DependencyBuilder<TPrincipalJob> RunJob<TJob>(object? parameter = null)
        where TJob : IJob
    {
        var jobDefinition = jobRegistry.FindJobDefinition(typeof(TJob));

        if (jobDefinition is null)
        {
            dependentJobOptions.Add(new JobDefinition(typeof(TJob), parameter, null, null));
        }
        else
        {
            dependentJobOptions.Add(jobDefinition with { Parameter = parameter });
        }

        return this;
    }

    /// <summary>
    /// Adds a job that runs after the principal job has finished with a given <paramref name="parameter"/>.
    /// <param name="jobName">The name of the registered job to run.</param>
    /// <param name="parameter">An optional parameter that is passed down as the <see cref="JobExecutionContext"/> to the job.</param>
    /// </summary>
    /// <remarks>
    /// </remarks>
    public DependencyBuilder<TPrincipalJob> RunJob(string jobName, object? parameter = null)
    {
        var jobDefinition = jobRegistry.FindJobDefinition(jobName);

        if (jobDefinition is not null)
        {
            dependentJobOptions.Add(jobDefinition with { Parameter = parameter });
            return this;
        }

        throw new InvalidOperationException(
            $"""
            Invalid job reference detected. Job named '{jobName}' hasn't been previously registered.
            """);
    }

    /// <summary>
    /// Adds an anonymous delegate job that runs after the principal job has finished.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed. This delegate must return either void or Task.</param>
    /// <param name="jobName">Sets the job name that can be used to identify and manipulate the job later on.</param>
    public DependencyBuilder<TPrincipalJob> RunJob(Delegate jobDelegate, string? jobName = null)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);

        var entry = jobRegistry.AddDynamicJob(jobDelegate, jobName);
        dependentJobOptions.Add(entry);

        return this;
    }

    internal List<JobDefinition> GetDependentJobOption() => dependentJobOptions;
}
