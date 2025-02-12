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

    public static void ShouldBeScheduledThenCompleted(this IList<ExecutionProgress> events)
    {
        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Scheduled);
        events[3].State.ShouldBe(ExecutionState.Initializing);
        events[4].State.ShouldBe(ExecutionState.Running);
        events[5].State.ShouldBe(ExecutionState.Completing);
        events[6].State.ShouldBe(ExecutionState.Completed);
        events[7].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(8);
    }

    public static void ShouldBeScheduledThenFaultedDuringInitialization(this IList<ExecutionProgress> events)
    {
        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Scheduled);
        events[3].State.ShouldBe(ExecutionState.Initializing);
        events[4].State.ShouldBe(ExecutionState.Faulted);
        events[5].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(6);
    }

    public static void ShouldBeScheduledThenFaultedDuringRun(this IList<ExecutionProgress> events)
    {
        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Scheduled);
        events[3].State.ShouldBe(ExecutionState.Initializing);
        events[4].State.ShouldBe(ExecutionState.Running);
        events[5].State.ShouldBe(ExecutionState.Faulted);
        events[6].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(7);
    }

    public static void ShouldBeInstantThenCompleted(this IList<ExecutionProgress> events)
    {
        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Initializing);
        events[3].State.ShouldBe(ExecutionState.Running);
        events[4].State.ShouldBe(ExecutionState.Completing);
        events[5].State.ShouldBe(ExecutionState.Completed);
        events[6].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(7);
    }

    public static void ShouldBeInstantThenFaultedDuringRun(this IList<ExecutionProgress> events)
    {
        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Initializing);
        events[3].State.ShouldBe(ExecutionState.Running);
        events[4].State.ShouldBe(ExecutionState.Faulted);
        events[5].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(6);
    }
}
