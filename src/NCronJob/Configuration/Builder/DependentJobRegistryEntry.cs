namespace NCronJob;

internal sealed class DependentJobRegistryEntry
{
    public required Type PrincipalType { get; init; }
    public List<JobDefinition> RunWhenSuccess { get; init; } = [];
    public List<JobDefinition> RunWhenFaulted { get; init; } = [];
}
