using Shouldly;

namespace NCronJob.Tests;

public static class EventsHelper
{
    public static IList<ExecutionProgress> FilterByOrchestrationId(
        this IList<ExecutionProgress> events,
        Guid orchestrationId)
    {
        return events.Where(e => e.CorrelationId == orchestrationId).ToList();
    }
}
