using System.Collections.Frozen;
using System.Collections.Immutable;

namespace LinkDotNet.NCronJob;

internal sealed class JobRegistry
{
    private readonly ImmutableArray<JobDefinition> cronJobs;
    private readonly FrozenDictionary<Type, List<DependentJobOption>> dependency;

    public JobRegistry(
        IEnumerable<JobDefinition> jobs,
        IEnumerable<DependentJobOption> dependentJobs)
    {
        cronJobs = [..jobs.Where(c => c.CronExpression is not null)];
        dependency = dependentJobs
            .GroupBy(d => d.PrincipalJobType)
            .ToFrozenDictionary(k => k.Key, v => v.ToList());
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => cronJobs;

    public List<DependentJobOption> GetDependencies(Type principalType)
        => dependency.TryGetValue(principalType, out var value)
            ? value
            : [];
}

