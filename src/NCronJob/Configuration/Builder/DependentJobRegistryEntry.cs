namespace NCronJob;

internal sealed class DependentJobRegistryEntry
{
    public List<JobDefinition> RunWhenSuccess { get; init; } = [];
    public List<JobDefinition> RunWhenFaulted { get; init; } = [];
}
