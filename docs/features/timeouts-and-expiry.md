# Job Timeouts and Run Expiry

NCronJob provides two independent time limits:

- **Execution timeout** limits how long a job run may execute.
- **Run expiry** limits how late a queued job may start.

## Execution timeout

Use `WithTimeout` to cancel a job when its execution exceeds a duration:

```csharp
builder.Services.AddNCronJob(options => options.AddJob<ImportJob>(job => job
    .WithCronExpression("0 * * * *")
    .WithTimeout(TimeSpan.FromMinutes(10))));
```

The timeout starts when NCronJob begins executing the job, after queueing and condition evaluation. The cancellation token passed to `IJob.RunAsync` is cancelled when the timeout elapses. The timeout covers retry attempts as one complete execution window rather than restarting for every attempt.

A timed-out run is reported as `Cancelled`. It does not trigger success or fault dependencies. Jobs should observe the supplied `CancellationToken` and stop promptly; NCronJob cannot forcibly terminate code that ignores cancellation.

Use `Timeout.InfiniteTimeSpan` to explicitly disable the timeout. Jobs have no execution timeout by default.

```csharp
builder.Services.AddNCronJob(options => options.AddJob<LongRunningJob>(job => job
    .WithTimeout(Timeout.InfiniteTimeSpan)
    .RunAtStartup()));
```

Timeouts can also be configured for dependent jobs:

```csharp
builder.Services.AddNCronJob(options => options
    .AddJob<ImportJob>(job => job.WithCronExpression("0 * * * *"))
    .ExecuteWhen(success: jobs => jobs
        .RunJob<TransformJob>()
        .WithTimeout(TimeSpan.FromMinutes(5))));
```

## Queued-run expiry

A run can become stale while waiting for its scheduled time or scheduler capacity. By default, NCronJob marks it as `Expired` when it starts more than ten minutes after its intended run time. An expired run is removed without executing its job body.

Configure the scheduler-wide grace period with `WithDefaultJobRunExpiry`:

```csharp
builder.Services.AddNCronJob(options => options
    .WithDefaultJobRunExpiry(TimeSpan.FromMinutes(30))
    .AddJob<ImportJob>(job => job.WithCronExpression("0 * * * *")));
```

Override it for an individual job with `WithJobRunExpiry`:

```csharp
builder.Services.AddNCronJob(options => options.AddJob<TimeSensitiveJob>(job => job
    .WithCronExpression("*/5 * * * *")
    .WithJobRunExpiry(TimeSpan.FromMinutes(2))));
```

`Timeout.InfiniteTimeSpan` disables expiry globally or for an individual job. Zero and other negative durations are rejected.

!!! note
    Expiry is not a misfire-recovery policy. An expired occurrence is skipped; NCronJob continues scheduling future cron occurrences normally.

## Choosing values

- Set an execution timeout longer than the normal job duration, including expected retries.
- Set run expiry according to how long the work remains useful after its scheduled time.
- Ensure external operations receive and observe the job cancellation token.
- Use infinite values only when another layer provides appropriate cancellation or stale-work protection.
