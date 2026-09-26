using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class TracingTests : JobIntegrationBase
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Activity>> stoppedActivities = new();
    private readonly ActivityListener listener;

    public TracingTests()
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NCronJobDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => StoppedActivityFor((string)activity.GetTagItem("ncronjob.correlation_id")!).TrySetResult(activity),
        };
        ActivitySource.AddActivityListener(listener);
    }

    [Fact]
    public async Task SuccessfulJobRunShouldEmitActivity()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>());
        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .RunInstantJob<DummyJob>(token: CancellationToken);

        var activity = await StoppedActivityFor(orchestrationId.ToString()).Task.WaitAsync(CancellationToken);

        activity.DisplayName.ShouldBe(typeof(DummyJob).FullName);
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
        activity.GetTagItem("ncronjob.job.name").ShouldBe(typeof(DummyJob).FullName);
        activity.GetTagItem("ncronjob.trigger_type").ShouldBe(nameof(TriggerType.Instant));
        activity.GetTagItem("ncronjob.attempts").ShouldBe(1);
    }

    [Fact]
    public async Task FailingJobRunShouldEmitErroredActivity()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ExceptionJob>());
        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .RunInstantJob<ExceptionJob>(token: CancellationToken);

        var activity = await StoppedActivityFor(orchestrationId.ToString()).Task.WaitAsync(CancellationToken);

        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            listener.Dispose();
        }

        base.Dispose(disposing);
    }

    private TaskCompletionSource<Activity> StoppedActivityFor(string correlationId) =>
        stoppedActivities.GetOrAdd(correlationId, _ => new TaskCompletionSource<Activity>(TaskCreationOptions.RunContinuationsAsynchronously));
}
