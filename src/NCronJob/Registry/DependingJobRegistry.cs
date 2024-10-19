using System.Collections.Immutable;

namespace NCronJob;

internal sealed class DependingJobRegistry
{
    private readonly ImmutableDictionary<Type, JobDefinition[]> success;
    private readonly ImmutableDictionary<Type, JobDefinition[]> faulted;

    public DependingJobRegistry(IEnumerable<DependentJobRegistryEntry> dependentJobRegistryEntries)
    {
        var jobRegistryEntries = dependentJobRegistryEntries as DependentJobRegistryEntry[] ?? dependentJobRegistryEntries.ToArray();
        success = jobRegistryEntries
            .GroupBy(d => d.PrincipalType)
            .ToImmutableDictionary(k => k.Key, v => v.SelectMany(p => p.RunWhenSuccess).ToArray());
        faulted = jobRegistryEntries
            .GroupBy(d => d.PrincipalType)
            .ToImmutableDictionary(k => k.Key, v => v.SelectMany(p => p.RunWhenFaulted).ToArray());
    }

    public IReadOnlyCollection<JobDefinition> GetSuccessTypes(Type principalType)
        => success.TryGetValue(principalType, out var types) ? types : [];

    public IReadOnlyCollection<JobDefinition> GetFaultedTypes(Type principalType)
        => faulted.TryGetValue(principalType, out var types) ? types : [];
}
