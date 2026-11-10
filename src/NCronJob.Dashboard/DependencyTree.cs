namespace NCronJob.Dashboard;

internal sealed record DependencyNode(string JobName, bool RunsOnFault, IReadOnlyList<DependencyNode> Dependents);

internal static class DependencyTree
{
    public static IReadOnlyList<DependencyNode> DependentsOf(JobRegistry registry, JobDefinition job) =>
        DependentsOf(registry, job, []);

    private static List<DependencyNode> DependentsOf(JobRegistry registry, JobDefinition job, HashSet<string> chain)
    {
        if (!chain.Add(job.Name))
        {
            return [];
        }

        var dependents = registry.GetDependentSuccessJobs(job).Select(j => ToNode(j, runsOnFault: false))
            .Concat(registry.GetDependentFaultedJobs(job).Select(j => ToNode(j, runsOnFault: true)))
            .ToList();

        chain.Remove(job.Name);
        return dependents;

        DependencyNode ToNode(JobDefinition dependent, bool runsOnFault) =>
            new(dependent.Name, runsOnFault, DependentsOf(registry, dependent, chain));
    }
}
