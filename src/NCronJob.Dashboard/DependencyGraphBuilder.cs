namespace NCronJob.Dashboard;

internal sealed class DependencyGraphBuilder
{
    private const double HorizontalSpacing = 260;
    private const double VerticalSpacing = 120;
    private readonly JobRegistry registry;

    public DependencyGraphBuilder(JobRegistry registry)
    {
        this.registry = registry;
    }

    public GraphModel Build()
    {
        var definitions = new Dictionary<string, JobDefinition>(StringComparer.Ordinal);
        var rawEdges = new HashSet<(string From, string To, GraphEdgeKind Kind)>();

        foreach (var root in registry.GetAllRootJobs())
        {
            Visit(root, definitions, rawEdges, new HashSet<string>(StringComparer.Ordinal));
        }

        var levels = ComputeLevels(definitions.Keys, rawEdges);
        var nodes = levels
            .GroupBy(pair => pair.Value)
            .OrderBy(group => group.Key)
            .SelectMany(group => group.OrderBy(pair => definitions[pair.Key].Name, StringComparer.Ordinal)
                .Select((pair, index) => new GraphNode(
                    pair.Key,
                    GetLabel(definitions[pair.Key]),
                    60 + (pair.Value * HorizontalSpacing),
                    60 + (index * VerticalSpacing),
                    pair.Value)))
            .ToArray();

        var edges = rawEdges.Select(edge => new GraphEdge(edge.From, edge.To, edge.Kind)).ToArray();
        return new GraphModel(nodes, edges);
    }

    private void Visit(
        JobDefinition definition,
        IDictionary<string, JobDefinition> definitions,
        ISet<(string From, string To, GraphEdgeKind Kind)> edges,
        ISet<string> path)
    {
        var id = GetId(definition);
        definitions.TryAdd(id, definition);
        if (!path.Add(id))
        {
            return;
        }

        AddDependents(definition, registry.GetDependentSuccessJobTypes(definition), GraphEdgeKind.Success, definitions, edges, path);
        AddDependents(definition, registry.GetDependentFaultedJobTypes(definition), GraphEdgeKind.Faulted, definitions, edges, path);
        path.Remove(id);
    }

    private void AddDependents(
        JobDefinition parent,
        IEnumerable<JobDefinition> dependents,
        GraphEdgeKind kind,
        IDictionary<string, JobDefinition> definitions,
        ISet<(string From, string To, GraphEdgeKind Kind)> edges,
        ISet<string> path)
    {
        var parentId = GetId(parent);
        foreach (var dependent in dependents)
        {
            var childId = GetId(dependent);
            edges.Add((parentId, childId, kind));
            Visit(dependent, definitions, edges, path);
        }
    }

    private static Dictionary<string, int> ComputeLevels(
        IEnumerable<string> nodeIds,
        IReadOnlyCollection<(string From, string To, GraphEdgeKind Kind)> edges)
    {
        var levels = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        for (var iteration = 0; iteration < levels.Count; iteration++)
        {
            var changed = false;
            foreach (var (from, to, _) in edges)
            {
                var next = Math.Min(levels.Count - 1, levels[from] + 1);
                if (next > levels[to])
                {
                    levels[to] = next;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return levels;
    }

    private static string GetId(JobDefinition definition)
        => $"{definition.JobFullName}|{definition.CustomName}|{ParameterSerializer.Serialize(definition.Parameter)}";

    private static string GetLabel(JobDefinition definition)
        => definition.CustomName ?? definition.Type?.Name ?? "Anonymous job";
}
