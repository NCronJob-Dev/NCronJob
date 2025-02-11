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

    public static void ShouldBeScheduledThenCancelled(this IList<ExecutionProgress> events)
    {
        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Scheduled);
        events[3].State.ShouldBe(ExecutionState.Cancelled);
        events[4].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(5);
    }
}
