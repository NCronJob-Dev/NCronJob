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

    private static void ShouldFollowStates(
        IList<ExecutionProgress> events,
        Type? type,
        string? name,
        params ExecutionState[] expectedStates)
    {
        for (var i = 0; i < expectedStates.Length; i++)
        {
            events[i].ShouldBeWellFormed(type, name, expectedStates[i]);
        }

        events.Count.ShouldBe(expectedStates.Length);
    }

    private static void ShouldBeScheduledThenCancelled(
        IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        ShouldFollowStates(events, type, name,
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Cancelled,
            ExecutionState.OrchestrationCompleted);
    }

    private static void ShouldBeScheduledThenCompleted(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        ShouldFollowStates(events, type, name,
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Initializing,
            ExecutionState.Running,
            ExecutionState.Completing,
            ExecutionState.Completed,
            ExecutionState.OrchestrationCompleted);
    }

    private static void ShouldBeScheduledThenFaultedDuringInitialization(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        ShouldFollowStates(events, type, name,
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Initializing,
            ExecutionState.Faulted,
            ExecutionState.OrchestrationCompleted);
    }

    private static void ShouldBeScheduledThenFaultedDuringRun(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        ShouldFollowStates(events, type, name,
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Initializing,
            ExecutionState.Running,
            ExecutionState.Faulted,
            ExecutionState.OrchestrationCompleted);
    }

    private static void ShouldBeInstantThenCompleted(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        ShouldFollowStates(events, type, name,
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Initializing,
            ExecutionState.Running,
            ExecutionState.Completing,
            ExecutionState.Completed,
            ExecutionState.OrchestrationCompleted);
    }

    private static void ShouldBeInstantThenFaultedDuringRun(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        ShouldFollowStates(events, type, name,
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Initializing,
            ExecutionState.Running,
            ExecutionState.Faulted,
            ExecutionState.OrchestrationCompleted);
    }

    private static void ShouldBeInstantThenExpired(
        this IList<ExecutionProgress> events,
        Type? type,
        string? name)
    {
        ShouldFollowStates(events, type, name,
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Expired,
            ExecutionState.OrchestrationCompleted);
    }

    private static void ShouldBeWellFormed(
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
    }
}
