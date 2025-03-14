namespace NCronJob;

internal class JobDefinitionCollector
{
    public Dictionary<JobDefinition, List<DependentJobRegistryEntry>> Entries { get; } = [];

    public void Add(JobDefinition jobDefinition)
    {
        Entries.GetOrCreateList(jobDefinition);
    }

    public void Add(IEnumerable<JobDefinition> jobDefinitions)
    {
        foreach (var jobDefinition in jobDefinitions)
        {
            Add(jobDefinition);
        }
    }

    public void Add(IEnumerable<JobDefinition> parentJobDefinitions, DependentJobRegistryEntry dependentJobs)
    {
        foreach (var jobDefinition in parentJobDefinitions)
        {
            var entries = Entries.GetOrCreateList(jobDefinition);
            entries.Add(dependentJobs);
        }
    }
}
