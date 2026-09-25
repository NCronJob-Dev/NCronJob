using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class AnonymousJobTests : JobIntegrationBase
{
    [Fact]
    public async Task MinimalJobApiCanBeUsedForTriggeringCronJobs()
    {
        ServiceCollection.AddNCronJob((Storage storage) =>
        {
            storage.Add("Done");
        }, Cron.AtEveryMinute);

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredOrchestrationEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredOrchestrationEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries[0].ShouldBe("Done");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnonymousJobsCanBeExecutedMultipleTimes()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob((Storage storage) =>
        {
            storage.Add("true");
        }, Cron.AtEveryMinute));

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeStepwiseUntilStateCount(
            TimeSpan.FromMinutes(1),
            steps: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(Events, e));

        Storage.Entries.ShouldAllBe(e => e == "true");
        Storage.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task StaticAnonymousJobsCanBeExecutedMultipleTimes()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(JobMethods.WriteTrueStaticAsync, Cron.AtEveryMinute));

        await StartNCronJob();

        var completedOrchestrationEvents = await AdvanceTimeStepwiseUntilStateCount(
            TimeSpan.FromMinutes(1),
            steps: 10,
            ExecutionState.OrchestrationCompleted,
            expectedCount: 10);

        completedOrchestrationEvents.ShouldAllBe(e => OrchestrationIsScheduledThenCompleted(Events, e));

        Storage.Entries.ShouldAllBe(e => e == "true");
        Storage.Entries.Count.ShouldBe(10);
    }

    private static bool OrchestrationIsScheduledThenCompleted(IList<ExecutionProgress> events, ExecutionProgress executionProgress)
    {
        var filteredEvents = events.FilterByOrchestrationId(executionProgress.CorrelationId);
        filteredEvents.ShouldBeScheduledThenCompleted();
        return true;
    }

    private static class JobMethods
    {
        public static Task WriteTrueStaticAsync(Storage storage)
        {
            storage.Add("true");

            return Task.CompletedTask;
        }
    }
}
