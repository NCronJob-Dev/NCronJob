using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobRemovalTests : JobIntegrationBase
{
    [Fact]
    public async Task CanRemoveJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob((Storage storage) => storage.Add("true"), Cron.AtEveryMinute, jobName: "Job"));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var orchestrationId = Events[0].CorrelationId;

        registry.RemoveJob("Job");

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCancelled("Job");
    }

    [Fact]
    public async Task RemovingAJobStopsItsQueueWorker()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var queueWorker = ServiceProvider.GetServices<IHostedService>().OfType<QueueWorker>().Single();

        registry.TryRegister(s => s.AddJob((Storage storage) => storage.Add("true"), Cron.AtEveryMinute, jobName: "Job"), out _).ShouldBeTrue();
        var queueName = queueWorker.GetActiveWorkerQueueNames().Single();

        registry.RemoveJob("Job");

        await queueWorker.WaitForWorkerRemovalAsync(queueName, CancellationToken);

        ServiceProvider.GetRequiredService<JobQueueManager>().GetAllJobQueueNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposingQueueWorkerStopsPendingWorkerRemovalWait()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob((Storage storage) => storage.Add("true"), Cron.AtEveryMinute, jobName: "Job"));

        await StartNCronJob();

        var queueWorker = ServiceProvider.GetServices<IHostedService>().OfType<QueueWorker>().Single();
        var queueName = queueWorker.GetActiveWorkerQueueNames().Single();
        var wait = queueWorker.WaitForWorkerRemovalAsync(queueName, CancellationToken);

        queueWorker.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(wait);
    }

    [Fact]
    public void CannotRemoveAUntypedDependentJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob(() => { }, "Job"))
        );

        var jobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var act = () => jobRegistry.RemoveJob("Job");

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldBe("Cannot remove a job that is a dependency of another job.");
    }

    [Fact]
    public void CannotRemoveATypedDependentJobByName()
    {
        ServiceCollection.AddNCronJob(
            s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>())
        );

        var jobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var act = () => jobRegistry.RemoveJob<AnotherDummyJob>();

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldBe("Cannot remove a job that is a dependency of another job.");
    }

    [Fact]
    public void DoesNotCringeWhenRemovingNonExistingJobs()
    {
        ServiceCollection.AddNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.GetAllRootJobs().ShouldBeEmpty();

        registry.RemoveJob("Nope");
        registry.RemoveJob<DummyJob>();
    }

    [Fact]
    public async Task CanRemoveByJobType()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<DummyJob>();

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

    [Fact]
    public async Task RemovingByJobTypeAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression("1 * * * *")));
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).Count.ShouldBe(2);

        registry.RemoveJob<DummyJob>();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).ShouldBeEmpty();

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

    [Fact]
    public async Task RemovingByJobTypeDisabledTypeJobsAccountsForAllJobs()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression("1 * * * *")));
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2)));

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).Count.ShouldBe(2);

        registry.DisableJob<DummyJob>();
        registry.RemoveJob<DummyJob>();

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        jobRegistry.FindAllRootJobDefinition(typeof(DummyJob)).ShouldBeEmpty();

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCancelled<DummyJob>();
    }

    [Fact]
    public async Task StoppingWaitsForRunningJobsOfRemovedQueues()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ServiceCollection.AddSingleton(gate);
        ServiceCollection.AddNCronJob(s => s.AddJob<GatedJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(ExecutionState.Running, 1);

        ServiceProvider.GetRequiredService<IRuntimeJobRegistry>().RemoveJob<GatedJob>();

        var queueWorker = ServiceProvider.GetServices<IHostedService>().OfType<QueueWorker>().Single();
        var stopTask = queueWorker.StopAsync(CancellationToken);

        stopTask.IsCompleted.ShouldBeFalse();

        gate.SetResult();
        await stopTask;
    }

    private sealed class GatedJob(TaskCompletionSource gate) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => gate.Task;
    }
}
