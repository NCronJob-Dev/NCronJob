using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Represents the builder for the dependent jobs.
/// </summary>
public sealed class DependencyBuilder<TPrincipalJob>
    where TPrincipalJob : IJob
{
    private readonly List<JobDefinition> dependentJobOptions = [];
    private readonly IServiceCollection services;

    internal DependencyBuilder(IServiceCollection services) => this.services = services;

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

    /// <summary>
    /// Adds an anonymous delegate job that runs after the principal job has finished.
    /// </summary>
    /// <param name="jobDelegate">The delegate that represents the job to be executed. This delegate must return either void or Task.</param>
    public DependencyBuilder<TPrincipalJob> RunJob(Delegate jobDelegate)
    {
        ArgumentNullException.ThrowIfNull(jobDelegate);

        var jobPolicyMetadata = new JobExecutionAttributes(jobDelegate);
        var entry = new JobDefinition(typeof(DynamicJobFactory), null, null, null,
            JobName: DynamicJobNameGenerator.GenerateJobName(jobDelegate),
            JobPolicyMetadata: jobPolicyMetadata);
        dependentJobOptions.Add(entry);
        services.AddSingleton(entry);
        services.AddSingleton(new DynamicJobRegistration(entry, sp => new DynamicJobFactory(sp, jobDelegate)));
        return this;
    }

    internal List<JobDefinition> GetDependentJobOption() => dependentJobOptions;
}
