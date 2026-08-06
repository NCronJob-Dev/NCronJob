using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Dashboard.Tests;

public class DashboardDataStoreTests
{
    [Fact]
    public async Task AggregatesRunningRecentAndOrchestrationRuns()
    {
        var observer = new JobExecutionProgressObserver();
        using var store = new DashboardDataStore(observer, new NCronJobDashboardOptions { MaxHistoryEntries = 2 });
        await store.StartAsync(TestContext.Current.CancellationToken);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var rootDefinition = JobDefinition.CreateTyped("root", typeof(RootJob), new { Region = "eu" });
        var childDefinition = JobDefinition.CreateTyped(typeof(ChildJob), null);
        var root = JobRun.CreateStartupJob(time, observer.Report, rootDefinition);

        root.NotifyStateChange(JobStateType.Initializing);
        root.NotifyStateChange(JobStateType.Running);
        store.GetRunningRuns().Single().ParameterJson.ShouldContain("eu");

        var child = root.CreateDependent(childDefinition, null, CancellationToken.None);
        child.NotifyStateChange(JobStateType.Initializing);
        child.NotifyStateChange(JobStateType.Completed);
        root.NotifyStateChange(JobStateType.Completed);

        store.GetRunningRuns().ShouldBeEmpty();
        store.GetRecentRuns().Count.ShouldBe(2);
        var orchestration = store.GetOrchestrations().Single();
        orchestration.Runs.Count.ShouldBe(2);
        orchestration.Runs.Single(run => run.ParentJobRunId is not null).ParentJobRunId.ShouldBe(root.JobRunId);
    }

    [Fact]
    public async Task CompletedRunsIgnoreFurtherUpdates()
    {
        var observer = new JobExecutionProgressObserver();
        using var store = new DashboardDataStore(observer, new NCronJobDashboardOptions());
        await store.StartAsync(TestContext.Current.CancellationToken);
        var run = JobRun.CreateStartupJob(new FakeTimeProvider(), observer.Report, JobDefinition.CreateTyped(typeof(RootJob), null));
        run.NotifyStateChange(JobStateType.Completed);
        observer.Report(run);

        store.GetRecentRuns().Single().State.ShouldBe(JobStateType.Completed);
    }

    private sealed class RootJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class ChildJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask;
    }
}
