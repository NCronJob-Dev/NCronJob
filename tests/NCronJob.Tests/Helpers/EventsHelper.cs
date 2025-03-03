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

    public static void ShouldBeScheduledThenCancelled<T>(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenCancelled(events, typeof(T), name);
    }

    public static void ShouldBeScheduledThenCancelled(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenCancelled(events, null, name);
    }

    public static void ShouldBeScheduledThenCompleted<T>(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenCompleted(events, typeof(T), name);
    }

    public static void ShouldBeScheduledThenCompleted(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenCompleted(events, null, name);
    }

    public static void ShouldBeScheduledThenFaultedDuringInitialization<T>(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenFaultedDuringInitialization(events, typeof(T), name);
    }

    public static void ShouldBeScheduledThenFaultedDuringInitialization(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenFaultedDuringInitialization(events, null, name);
    }

    public static void ShouldBeScheduledThenFaultedDuringRun<T>(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenFaultedDuringRun(events, typeof(T), name);
    }

    public static void ShouldBeScheduledThenFaultedDuringRun(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeScheduledThenFaultedDuringRun(events, null, name);
    }

    public static void ShouldBeInstantThenCompleted<T>(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeInstantThenCompleted(events, typeof(T), name);
    }

    public static void ShouldBeInstantThenCompleted(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeInstantThenCompleted(events, null, name);
    }

    public static void ShouldBeInstantThenFaultedDuringRun<T>(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeInstantThenFaultedDuringRun(events, typeof(T), name);
    }

    public static void ShouldBeInstantThenFaultedDuringRun(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeInstantThenFaultedDuringRun(events, null, name);
    }

    public static void ShouldBeInstantThenExpired<T>(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeInstantThenExpired(events, typeof(T), name);
    }

    public static void ShouldBeInstantThenExpired(
        this IList<ExecutionProgress> events,
        string? name = null)
    {
        ShouldBeInstantThenExpired(events, null, name);
    }

    private static void ShouldBeScheduledThenCancelled(
        IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        events[0].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationStarted);
        events[1].ShouldBeWellFormed(type, name, ExecutionState.NotStarted);
        events[2].ShouldBeWellFormed(type, name, ExecutionState.Scheduled);
        events[3].ShouldBeWellFormed(type, name, ExecutionState.Cancelled);
        events[4].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(5);
    }

    private static void ShouldBeScheduledThenCompleted(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        events[0].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationStarted);
        events[1].ShouldBeWellFormed(type, name, ExecutionState.NotStarted);
        events[2].ShouldBeWellFormed(type, name, ExecutionState.Scheduled);
        events[3].ShouldBeWellFormed(type, name, ExecutionState.Initializing);
        events[4].ShouldBeWellFormed(type, name, ExecutionState.Running);
        events[5].ShouldBeWellFormed(type, name, ExecutionState.Completing);
        events[6].ShouldBeWellFormed(type, name, ExecutionState.Completed);
        events[7].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(8);
    }

    private static void ShouldBeScheduledThenFaultedDuringInitialization(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        events[0].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationStarted);
        events[1].ShouldBeWellFormed(type, name, ExecutionState.NotStarted);
        events[2].ShouldBeWellFormed(type, name, ExecutionState.Scheduled);
        events[3].ShouldBeWellFormed(type, name, ExecutionState.Initializing);
        events[4].ShouldBeWellFormed(type, name, ExecutionState.Faulted);
        events[5].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(6);
    }

    private static void ShouldBeScheduledThenFaultedDuringRun(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        events[0].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationStarted);
        events[1].ShouldBeWellFormed(type, name, ExecutionState.NotStarted);
        events[2].ShouldBeWellFormed(type, name, ExecutionState.Scheduled);
        events[3].ShouldBeWellFormed(type, name, ExecutionState.Initializing);
        events[4].ShouldBeWellFormed(type, name, ExecutionState.Running);
        events[5].ShouldBeWellFormed(type, name, ExecutionState.Faulted);
        events[6].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(7);
    }

    private static void ShouldBeInstantThenCompleted(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        events[0].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationStarted);
        events[1].ShouldBeWellFormed(type, name, ExecutionState.NotStarted);
        events[2].ShouldBeWellFormed(type, name, ExecutionState.Initializing);
        events[3].ShouldBeWellFormed(type, name, ExecutionState.Running);
        events[4].ShouldBeWellFormed(type, name, ExecutionState.Completing);
        events[5].ShouldBeWellFormed(type, name, ExecutionState.Completed);
        events[6].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(7);
    }

    private static void ShouldBeInstantThenFaultedDuringRun(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        events[0].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationStarted);
        events[1].ShouldBeWellFormed(type, name, ExecutionState.NotStarted);
        events[2].ShouldBeWellFormed(type, name, ExecutionState.Initializing);
        events[3].ShouldBeWellFormed(type, name, ExecutionState.Running);
        events[4].ShouldBeWellFormed(type, name, ExecutionState.Faulted);
        events[5].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(6);
    }

    private static void ShouldBeInstantThenExpired(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        events[0].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationStarted);
        events[1].ShouldBeWellFormed(type, name, ExecutionState.NotStarted);
        events[2].ShouldBeWellFormed(type, name, ExecutionState.Expired);
        events[3].ShouldBeWellFormed(type, name, ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(4);
    }

    private static bool ShouldBeWellFormed(
        this ExecutionProgress @event,
        Type? type,
        string? name,
        ExecutionState expectedExecutionState)
    {
        @event.State.ShouldBe(expectedExecutionState);

        switch (@event.State)
        {
            case ExecutionState.OrchestrationStarted:
            case ExecutionState.OrchestrationCompleted:
                @event.RunId.ShouldBeNull();
                @event.Name.ShouldBeNull();
                @event.Type.ShouldBeNull();
                @event.IsTypedJob.ShouldBeNull();
                break;

            default:
                @event.RunId.ShouldNotBeNull();
                @event.Name.ShouldBe(name);
                @event.Type.ShouldBe(type);
                @event.IsTypedJob.ShouldBe(@event.Type != null);
                break;
        }

        return true;
    }
}
