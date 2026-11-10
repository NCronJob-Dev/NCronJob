# NCronJob Compact Reference

This file is a compact Markdown entry point for assistants, automation, and quick retrieval.

## What NCronJob is

NCronJob is an in-process .NET scheduler for recurring jobs, startup jobs, and manually triggered jobs. It is intended for applications that want a native `IHostedService`-based scheduler without adding a database-backed platform such as Hangfire or Quartz.

## Main concepts

- **Typed jobs** implement `IJob`
- **Minimal jobs** are delegates registered via `AddNCronJob(...)`
- **Recurring jobs** use cron expressions
- **Instant jobs** are triggered through `IInstantJobRegistry`
- **Startup jobs** use `RunAtStartup(...)`
- **Runtime-managed jobs** use `IRuntimeJobRegistry`

## Primary APIs

### Register jobs

- `AddNCronJob(...)` for typed jobs and builder-based registration
- `AddNCronJob(Delegate jobDelegate, string cronExpression, TimeZoneInfo? timeZoneInfo = null)`
- `AddNCronJob(Delegate jobDelegate, string cronExpression, TimeZoneInfo? timeZoneInfo, string jobName)`

### Start startup jobs

- `UseNCronJobAsync(IHost)`
- `UseNCronJob(IHost)`

Only required when at least one job uses `RunAtStartup(...)`.

### Trigger work manually

- `IInstantJobRegistry.RunInstantJob(...)`
- `IInstantJobRegistry.RunScheduledJob(...)`
- `IInstantJobRegistry.ForceRunInstantJob(...)`
- `IInstantJobRegistry.ForceRunScheduledJob(...)`

### Manage recurring jobs at runtime

- `IRuntimeJobRegistry.TryRegister(...)`
- `IRuntimeJobRegistry.RemoveJob(...)`
- `IRuntimeJobRegistry.UpdateSchedule(...)`
- `IRuntimeJobRegistry.UpdateParameter(...)`
- `IRuntimeJobRegistry.TryGetSchedule(...)`
- `IRuntimeJobRegistry.TryGetNextOccurrence(...)`
- `IRuntimeJobRegistry.EnableJob(...)`
- `IRuntimeJobRegistry.DisableJob(...)`

### Configure scheduling limits

- `WithTimeout(TimeSpan)` on a job cancels a run once its execution exceeds the duration (retries share one window); `Timeout.InfiniteTimeSpan` disables it. A timed-out run is reported as `Cancelled` and triggers no success or fault dependencies. Also available for dependent jobs and startup jobs
- `WithJobRunExpiry(TimeSpan)` on a job and `WithDefaultJobRunExpiry(TimeSpan)` on the options limit how late a queued run may start (default: 10 minutes); expired runs are dropped without executing
- `SkipIfStillRunning()` on a job drops a cron occurrence as `Skipped` when the job is still running at its own concurrency limit, instead of queueing it
- `WithMaxDegreeOfParallelism(int)` on the options builder sets the scheduler-wide maximum of concurrently executing jobs; the value must be greater than zero

### Cron expressions and macros

`WithCronExpression(...)` and `AddNCronJob(Delegate, ...)` accept 5-part expressions, 6-part expressions with seconds, or one of these macros (evaluated in the configured time zone):

| Macro | Equivalent |
|---|---|
| `@every_second` | `* * * * * *` |
| `@every_minute` | `* * * * *` |
| `@hourly` | `0 * * * *` |
| `@daily` / `@midnight` | `0 0 * * *` |
| `@weekly` | `0 0 * * 0` |
| `@monthly` | `0 0 1 * *` |
| `@yearly` / `@annually` | `0 0 1 1 *` |

Unknown macros throw an `ArgumentException`.

`CronSchedule.IsValid(expression, out error)` validates an expression with the same rules, and `CronSchedule.GetNextOccurrences(expression, count, from, timeZone)` previews its upcoming UTC occurrences.

### Observability

- **Log scopes**: every job run executes in a log scope, so all entries written during the run (including the job's own `ILogger<T>`) carry `JobName`, `JobRunId`, `CorrelationId` and `TriggerType` (`Cron`, `Instant`, `Startup` or `Dependent`). Scopes must be enabled in the logging provider, e.g. `IncludeScopes` for the console logger
- **Tracing**: each job run is an `Activity` from the `ActivitySource` named `NCronJobDiagnostics.ActivitySourceName` (`"NCronJob"`). Subscribe with `AddOpenTelemetry().WithTracing(t => t.AddSource(NCronJobDiagnostics.ActivitySourceName))`. Tags: `ncronjob.job.name`, `ncronjob.correlation_id`, `ncronjob.trigger_type`, `ncronjob.attempts` and `error.type` on failure; a failed run sets the status to `Error`
- **Metrics**: a `Meter` named `NCronJobDiagnostics.MeterName` (`"NCronJob"`). Subscribe with `AddOpenTelemetry().WithMetrics(m => m.AddMeter(NCronJobDiagnostics.MeterName))`. Counters `ncronjob.job.runs`, `ncronjob.job.failures`, `ncronjob.job.retries`, `ncronjob.job.skipped`, `ncronjob.job.expired` and histogram `ncronjob.job.duration` (seconds, including retries), tagged with `ncronjob.job.name` and `ncronjob.trigger_type`
- **Progress**: `IJobExecutionProgressReporter` observes run state changes, including each run's `Parameter` and `TriggerType`
- **Dashboard**: the `NCronJob.Dashboard` package adds `services.AddNCronJobDashboard()` and `app.MapNCronJobDashboard("/ncronjob", e => e.RequireAuthorization(...))`. It is a Blazor Server UI for jobs, startup jobs, dependencies, live runs and triggering jobs with a JSON parameter. Run history is in memory only

## Practical rules

- Name jobs when you need runtime management
- Name jobs when the same job type is registered more than once
- Use `RunInstantJob("name")` to avoid ambiguity with duplicate job types
- Parameters are passed by reference and are not serialized
- Parameters are `object?` and not type-checked; validate `context.Parameter` at the start of the job
- Forced instant and scheduled jobs bypass queueing and concurrency safeguards
- Startup jobs require `UseNCronJobAsync()` or `UseNCronJob()`
- In tests, register `FakeTimeProvider` before `AddNCronJob` and `Advance` it to fire cron runs
- Jobs have no execution timeout by default; jobs must observe the `CancellationToken` for `WithTimeout` to take effect

## Best starting points

- First setup: [Getting Started](https://docs.ncronjob.dev/getting-started/)
- Docs guide: [Documentation Map](https://docs.ncronjob.dev/documentation-map/)
- Agents: [Agent & Automation Guide](https://docs.ncronjob.dev/agent-guide/)

## Feature index

- [Define and Schedule Jobs](https://docs.ncronjob.dev/features/define-and-schedule-jobs/)
- [Passing Parameters](https://docs.ncronjob.dev/features/parameters/)
- [Triggering instant jobs](https://docs.ncronjob.dev/features/instant-jobs/)
- [Concurrency control](https://docs.ncronjob.dev/features/concurrency-control/)
- [Global concurrency](https://docs.ncronjob.dev/advanced/global-concurrency/)
- [Job timeouts and run expiry](https://docs.ncronjob.dev/features/timeouts-and-expiry/)
- [Retry support](https://docs.ncronjob.dev/features/retry-support/)
- [Minimal API](https://docs.ncronjob.dev/features/minimal-api/)
- [Running Startup Jobs](https://docs.ncronjob.dev/features/startup-jobs/)
- [Model Dependencies](https://docs.ncronjob.dev/features/model-dependencies/)
- [Notifications](https://docs.ncronjob.dev/features/notifications/)
- [Conditional Job Scheduling](https://docs.ncronjob.dev/features/conditional-job-scheduling/)
- [Dynamic Job Control](https://docs.ncronjob.dev/advanced/dynamic-job-control/)
- [Observing job progress, tracing and metrics](https://docs.ncronjob.dev/advanced/observing-job-progress/)
- [Log level and log scopes](https://docs.ncronjob.dev/advanced/log-level/)
- [Testing jobs and schedules](https://docs.ncronjob.dev/advanced/testing/)
- [Known gotchas](https://docs.ncronjob.dev/advanced/known-gotchas/)
- [Dashboard](https://docs.ncronjob.dev/features/dashboard/)

## Sample projects

- `sample/MinimalSample` — https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/MinimalSample
- `sample/NCronJobSample` — https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/NCronJobSample
- `sample/RunOnceSample` — https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/RunOnceSample
