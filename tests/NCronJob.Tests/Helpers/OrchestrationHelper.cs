using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace NCronJob.Tests;

public class OrchestrationHelper
{
    private readonly FakeTimeProvider fakeTimeProvider;
    private readonly Action eventsMonitoringStopper;
    private readonly CancellationToken cancellationToken;

    public OrchestrationHelper(
        FakeTimeProvider fakeTimeProvider,
        Action eventsMonitoringStopper,
        CancellationToken cancellationToken)
    {
        this.fakeTimeProvider = fakeTimeProvider;
        this.eventsMonitoringStopper = eventsMonitoringStopper;
        this.cancellationToken = cancellationToken;
    }

    public (IDisposable subscription, IList<ExecutionProgress> events) RegisterAnExecutionProgressSubscriber(
        IServiceProvider serviceProvider)
    {
        SynchronizedCollection<ExecutionProgress> events = [];

        void Subscriber(ExecutionProgress progress)
        {
            events.Add(progress);
        }

        var progressReporter = serviceProvider.GetRequiredService<IJobExecutionProgressReporter>();

        return (progressReporter.Register(Subscriber), events);
    }

    public async Task<IList<ExecutionProgress>> WaitForNthOrchestrationState(
        IList<ExecutionProgress> events,
        ExecutionState state,
        int howMany,
        Action? onAnyFoundButLast = null,
        bool stopMonitoringEvents = false)
    {
        List<ExecutionProgress> seen = new();

        await WaitUntilConditionIsMet(events, LastOfNthCompletedOrchestration, stopMonitoringEvents);

        return seen;

        bool LastOfNthCompletedOrchestration(ExecutionProgress @event)
        {
            if (@event.State != state)
            {
                return false;
            }

            seen.Add(@event);

            if (onAnyFoundButLast is not null && seen.Count < howMany)
            {
                onAnyFoundButLast();
            }

            return seen.Count == howMany;
        }
    }

    public async Task WaitForOrchestrationState(
        IList<ExecutionProgress> events,
        Guid orchestrationId,
        ExecutionState state,
        bool stopMonitoringEvents = false)
    {
        await WaitUntilConditionIsMet(
            events,
            OrchestrationHasReachedExpectedState,
            stopMonitoringEvents);

        bool OrchestrationHasReachedExpectedState(ExecutionProgress @event)
        {
            return @event.CorrelationId == orchestrationId && @event.State == state;
        }
    }

    public async Task<ExecutionProgress> WaitUntilConditionIsMet(
        IList<ExecutionProgress> events,
        Func<ExecutionProgress, bool> evaluator,
        bool stopMonitoringEvents = false)
    {
        // Note: Although this function could seem a bit over-engineered, it's sadly necessary.
        // Indeed, events may actually be updated upstream while it's being enumerated
        // in here (which leads to a "Collection was modified; enumeration operation may not
        // execute." error message would we using any enumerating based (eg. Linq) traversal.

        int index = 0;

        while (true)
        {
            int count = events.Count;

            while (index < count)
            {
                if (evaluator(events[index]))
                {
                    if (stopMonitoringEvents)
                    {
                        eventsMonitoringStopper();
                    }

                    return events[index];
                }

                index++;
            }

            fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }
}
