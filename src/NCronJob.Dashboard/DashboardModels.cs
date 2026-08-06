namespace NCronJob.Dashboard;

internal sealed record DashboardRun(
    Guid JobRunId,
    Guid CorrelationId,
    Guid? ParentJobRunId,
    string JobName,
    string JobType,
    JobStateType State,
    DateTimeOffset StateChangedAt,
    TriggerType TriggerType,
    string ParameterJson);

internal sealed record OrchestrationSnapshot(Guid CorrelationId, IReadOnlyList<DashboardRun> Runs);

internal sealed record ScheduleEntry(
    string? Name,
    Type? Type,
    string DisplayName,
    string CronExpression,
    TimeZoneInfo TimeZone,
    DateTimeOffset? NextOccurrence,
    bool IsEnabled,
    string ParameterJson);

internal enum GraphEdgeKind
{
    Success,
    Faulted,
}

internal sealed record GraphNode(string Id, string Label, double X, double Y, int Level);

internal sealed record GraphEdge(string From, string To, GraphEdgeKind Kind);

internal sealed record GraphModel(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges);
