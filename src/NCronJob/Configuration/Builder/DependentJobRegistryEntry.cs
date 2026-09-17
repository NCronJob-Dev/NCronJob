namespace NCronJob;

internal sealed class DependentJobRegistryEntry
{
    public List<DependentJobDefinition> RunWhenSuccess { get; init; } = [];
    public List<DependentJobDefinition> RunWhenFaulted { get; init; } = [];
}
