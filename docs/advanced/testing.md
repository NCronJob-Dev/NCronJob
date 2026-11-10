# Testing jobs and schedules

NCronJob takes all time from the registered `TimeProvider`: cron timers, delays of scheduled instant jobs, retry back-off, timeouts and expiry. Swap it for a `FakeTimeProvider` and tests can fire cron jobs without waiting for real time to pass.

## Unit testing the job itself

Start here. `IJob<TParameter>` and `IJobExecutionContext<TParameter>` are interfaces, so a job's logic can be tested by calling the typed `RunAsync` directly: no host, no scheduler, no time provider. Any mocking library or a hand-written fake works:

```csharp
[Fact]
public async Task DeletesExpiredFiles()
{
    var context = Substitute.For<IJobExecutionContext<string>>();
    context.Parameter.Returns("/uploads");
    var job = new CleanupJob(new InMemoryFileStore(expiredFiles: 2));

    await job.RunAsync(context, CancellationToken.None);

    context.Output.ShouldBe(2);
}
```

Reach for the host setup below only when the test is about scheduling: cron timing, retries, timeouts, concurrency or dependencies between jobs.

## Driving cron jobs with `FakeTimeProvider`

Add the `Microsoft.Extensions.TimeProvider.Testing` package to the test project. Register the fake **before** `AddNCronJob`, because NCronJob only adds `TimeProvider.System` when no provider is registered yet.

```csharp
public sealed class CleanupJobTests
{
    [Fact]
    public async Task RunsEveryHour()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddSingleton(ran);
        builder.Services.AddNCronJob(o => o.AddJob<CleanupJob>(j => j.WithCronExpression("0 * * * *")));

        using var host = builder.Build();
        await host.StartAsync();

        time.Advance(TimeSpan.FromHours(1));

        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();
    }
}

public sealed class CleanupJob(TaskCompletionSource ran) : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        ran.TrySetResult();
        return Task.CompletedTask;
    }
}
```

`Advance` fires the cron timer, but the job itself runs on the thread pool. Wait for a signal the job sets (as above), a [notification handler](../features/notifications.md), or the [progress observer](observing-job-progress.md). Don't assert right after `Advance`.

## Checking a schedule without starting the host

`CronSchedule` uses the same parser as `WithCronExpression`, so unit tests can check an expression and its occurrences on their own:

```csharp
CronSchedule.IsValid("0 3 * * 1-5", out var error).ShouldBeTrue();

CronSchedule.GetNextOccurrences("0 3 * * 1-5", 3, from: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero))
    .ShouldBe([
        new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 5, 3, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 6, 3, 0, 0, TimeSpan.Zero),
    ]);
```

`GetNextOccurrences` accepts an optional `TimeZoneInfo` and returns UTC times. An unreachable expression such as `0 0 30 2 *` returns an empty list. To preview a registered job, read its expression and time zone with `IRuntimeJobRegistry.TryGetSchedule` and pass them in.

## Scheduling rules worth asserting

- **Overlap**: a cron occurrence that comes due while the same job is at its [concurrency limit](../features/concurrency-control.md) waits in the queue. With `SkipIfStillRunning()` it's reported as `Skipped` instead.
- **Missed occurrences collapse**: NCronJob schedules the next occurrence only after the current one starts, so a run that was held back never builds up a backlog. Missed slots are not replayed.
- **Expiry**: a queued run that starts later than its [run expiry](../features/timeouts-and-expiry.md) (10 minutes by default) is reported as `Expired` and not executed.
- **Shutdown**: stopping the host stops scheduling new runs and waits for running jobs to finish. Jobs should observe their `CancellationToken`.
- **No persistence**: schedules and run state live in memory and start fresh on every host start.
