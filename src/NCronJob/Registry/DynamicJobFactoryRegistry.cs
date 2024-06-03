namespace NCronJob;

internal sealed class DynamicJobFactoryRegistry
{
    private readonly JobRegistry jobRegistry;
    private readonly Dictionary<string, Func<IServiceProvider, DynamicJobFactory>> map;

    public DynamicJobFactoryRegistry(
        IEnumerable<DynamicJobRegistration> entries,
        JobRegistry jobRegistry)
    {
        this.jobRegistry = jobRegistry;
        map = entries.ToDictionary(e => e.JobDefinition.JobFullName, v => v.DynamicJobFactoryResolver);
    }

    public JobDefinition Add(Delegate jobDelegate)
    {
        var jobPolicyMetadata = new JobExecutionAttributes(jobDelegate);
        var entry = new JobDefinition(typeof(DynamicJobFactory), null, null, null,
            JobName: DynamicJobNameGenerator.GenerateJobName(jobDelegate),
            JobPolicyMetadata: jobPolicyMetadata);
        jobRegistry.Add(entry);
        map[entry.JobFullName] = serviceProvider => new DynamicJobFactory(serviceProvider, jobDelegate);

        return entry;
    }

    public void Add(DynamicJobRegistration entry)
    {
        jobRegistry.Add(entry.JobDefinition);
        map[entry.JobDefinition.JobFullName] = entry.DynamicJobFactoryResolver;
    }

    /// <summary>
    /// Gets the job instance.
    /// </summary>
    public IJob GetJobInstance(IServiceProvider serviceProvider, JobDefinition jobDefinition) 
        => map[jobDefinition.JobFullName](serviceProvider);
}
