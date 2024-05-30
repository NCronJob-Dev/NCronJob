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

    public JobDefinition Add(Delegate jobAction)
    {
        var jobPolicyMetadata = new JobExecutionAttributes(jobAction);
        var entry = new JobDefinition(typeof(DynamicJobFactory), null, null, null,
            JobName: DynamicJobNameGenerator.GenerateJobName(jobAction),
            JobPolicyMetadata: jobPolicyMetadata);
        jobRegistry.Add(entry);
        map[entry.JobFullName] = serviceProvider => new DynamicJobFactory(serviceProvider, jobAction);

        return entry;
    }

    public IJob GetAndDrainJobInstance(IServiceProvider serviceProvider, JobDefinition jobDefinition)
    {
        var element = map[jobDefinition.JobFullName](serviceProvider);
        map.Remove(jobDefinition.JobFullName);
        return element;
    }
}
