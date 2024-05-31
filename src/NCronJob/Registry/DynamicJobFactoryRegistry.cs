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

    /// <summary>
    /// Gets the job instance and removes it from the registry. The instance will be drained so that the Garbage Collector can collect it.
    /// </summary>
    /// <remarks>
    /// This function is called for triggering instant jobs. As the time interval between executions can be long (to indefinite),
    /// the job instance should be removed from the registry to prevent memory leaks.
    /// </remarks>
    public IJob GetAndDrainJobInstance(IServiceProvider serviceProvider, JobDefinition jobDefinition)
    {
        var element = map[jobDefinition.JobFullName](serviceProvider);
        map.Remove(jobDefinition.JobFullName);
        return element;
    }
}
