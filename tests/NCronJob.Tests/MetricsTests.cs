using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public sealed class MetricsTests : JobIntegrationBase
{
    private readonly ConcurrentQueue<(string Instrument, double Value, string? Trigger)> measurements = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> measured = new();
    private readonly MeterListener listener = new();
    private string observedJobName = string.Empty;

    public MetricsTests()
    {
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == NCronJobDiagnostics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start();
    }

    [Fact]
    public async Task SuccessfulRunShouldBeCountedAndTimed()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SuccessfulJob>());
        await StartNCronJob();
        Observe<SuccessfulJob>();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SuccessfulJob>(token: CancellationToken);
        await WaitForMeasurement("ncronjob.job.duration");

        Sum("ncronjob.job.runs").ShouldBe(1);
        Sum("ncronjob.job.failures").ShouldBe(0);
        measurements.ShouldContain(m => m.Instrument == "ncronjob.job.duration" && m.Value >= 0);
        measurements.ShouldAllBe(m => m.Trigger == nameof(TriggerType.Instant));
    }

    [Fact]
    public async Task FailingRunShouldBeCountedAsFailure()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJob>());
        await StartNCronJob();
        Observe<FailingJob>();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<FailingJob>(token: CancellationToken);
        await WaitForMeasurement("ncronjob.job.failures");

        Sum("ncronjob.job.runs").ShouldBe(1);
        Sum("ncronjob.job.failures").ShouldBe(1);
        measurements.ShouldContain(m => m.Instrument == "ncronjob.job.duration" && m.Value >= 0);
    }

    [Fact]
    public async Task RetriesShouldBeCounted()
    {
        Observe<RetryingJob>();
        ServiceCollection.AddNCronJob(n => n.AddJob<RetryingJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await AdvanceTimeUntilOrchestrationCompletion(Events[0].CorrelationId);
        await WaitForMeasurement("ncronjob.job.failures");

        Sum("ncronjob.job.runs").ShouldBe(1);
        Sum("ncronjob.job.retries").ShouldBe(2);
        Sum("ncronjob.job.failures").ShouldBe(1);
    }

    [Fact]
    public async Task SkippedRunShouldBeCounted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SuccessfulJob>(p => p.OnlyIf(() => false)));
        await StartNCronJob();
        Observe<SuccessfulJob>();

        ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SuccessfulJob>(token: CancellationToken);
        await WaitForMeasurement("ncronjob.job.skipped");

        Sum("ncronjob.job.skipped").ShouldBe(1);
        Sum("ncronjob.job.runs").ShouldBe(0);
    }

    [Fact]
    public void ExpiredRunShouldBeCounted()
    {
        Observe<SuccessfulJob>();
        var definition = JobDefinition.CreateTyped(typeof(SuccessfulJob), null);
        var run = JobRun.CreateStartupJob(new FakeTimeProvider(), _ => { }, definition);

        run.NotifyStateChange(JobStateType.Expired);
        run.NotifyStateChange(JobStateType.Expired);

        Sum("ncronjob.job.expired").ShouldBe(1);
        Sum("ncronjob.job.runs").ShouldBe(0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            listener.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Observe<TJob>() => observedJobName = typeof(TJob).FullName!;

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        string? jobName = null;
        string? trigger = null;
        foreach (var (key, tagValue) in tags)
        {
            if (key == "ncronjob.job.name") jobName = (string?)tagValue;
            if (key == "ncronjob.trigger_type") trigger = (string?)tagValue;
        }

        if (jobName != observedJobName)
        {
            return;
        }

        measurements.Enqueue((instrument.Name, value, trigger));
        MeasurementSignal(instrument.Name).TrySetResult();
    }

    private double Sum(string instrument) =>
        measurements.Where(m => m.Instrument == instrument).Sum(m => m.Value);

    private Task WaitForMeasurement(string instrument) =>
        MeasurementSignal(instrument).Task.WaitAsync(CancellationToken);

    private TaskCompletionSource MeasurementSignal(string instrument) =>
        measured.GetOrAdd(instrument, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    private sealed class SuccessfulJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class FailingJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => throw new InvalidOperationException();
    }

    [RetryPolicy(retryCount: 2, PolicyType.FixedInterval)]
    private sealed class RetryingJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => throw new InvalidOperationException();
    }
}
