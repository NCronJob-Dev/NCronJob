# Agent & Automation Guide

This page is a compact entry point for assistants, scripts, and contributors who need the main **NCronJob** concepts quickly.

## Primary public entry points

### Registration

- `AddNCronJob(...)` registers the scheduler and typed jobs
- `AddNCronJob(Delegate jobDelegate, string cronExpression, TimeZoneInfo? timeZoneInfo = null)` registers a delegate-based recurring job
- `AddNCronJob(Delegate jobDelegate, string cronExpression, TimeZoneInfo? timeZoneInfo, string jobName)` registers a named delegate-based recurring job

### Startup behavior

- `UseNCronJobAsync(IHost)` and `UseNCronJob(IHost)` must be called when any job uses `RunAtStartup(...)`
- For regular recurring jobs and instant jobs, these calls are optional

### Job authoring

- `IJob` is the main typed-job abstraction
- `IJobExecutionContext` provides the parameter, attempt count, correlation id, and execution metadata
- The minimal API style resolves services directly from the delegate signature

### Scheduling options

- `WithCronExpression(...)` accepts cron expressions (with optional seconds) and macros such as `@hourly`, `@daily`, `@weekly`, `@monthly`, `@yearly`, `@every_minute` and `@every_second`; see [Cron macros](features/define-and-schedule-jobs.md#cron-macros)
- `WithTimeout(TimeSpan)` cancels a run that executes too long; see [Timeouts and run expiry](features/timeouts-and-expiry.md)
- `WithMaxDegreeOfParallelism(int)` limits concurrently executing jobs scheduler-wide; see [Global concurrency](advanced/global-concurrency.md)

### Observability

- Every run executes in a log scope with `JobName`, `JobRunId`, `CorrelationId` and `TriggerType`; see [Log level and log scopes](advanced/log-level.md#log-scopes)
- Every run is an `Activity` from the `"NCronJob"` `ActivitySource` (`NCronJobDiagnostics.ActivitySourceName`); see [Tracing with OpenTelemetry](advanced/observing-job-progress.md#tracing-with-opentelemetry)
- Runs, failures, retries, skipped/expired runs and run duration are metrics of the `"NCronJob"` `Meter` (`NCronJobDiagnostics.MeterName`); see [Metrics with OpenTelemetry](advanced/observing-job-progress.md#metrics-with-opentelemetry)
- `NCronJob.Dashboard` package: `services.AddNCronJobDashboard()` + `app.MapNCronJobDashboard(path, configureEndpoints)` serve a Blazor Server dashboard; see [Dashboard](features/dashboard.md)

### Runtime interaction

- `IInstantJobRegistry` triggers instant and delayed jobs
- `IRuntimeJobRegistry` adds, removes, enables, disables, and updates named recurring jobs at runtime

## Recommended reading order for agents

1. [Getting Started](getting-started.md)
2. [Define and Schedule Jobs](features/define-and-schedule-jobs.md)
3. [Triggering instant jobs](features/instant-jobs.md)
4. [Timeouts and run expiry](features/timeouts-and-expiry.md)
5. [Global concurrency](advanced/global-concurrency.md)
6. [Dynamic Job Control](advanced/dynamic-job-control.md)
7. [Observing job progress](advanced/observing-job-progress.md) and [log scopes](advanced/log-level.md)
8. [Known gotchas](advanced/known-gotchas.md)

## High-signal behavioral notes

- Use **named jobs** when the same job type is registered multiple times or when runtime management is needed
- `RunInstantJob<TJob>()` can become ambiguous if multiple registrations exist for the same job type; prefer `RunInstantJob("job-name")` in that case
- Parameters are passed by reference, not serialized, so mutation after enqueueing can affect execution
- `context.Parameter` is `object?` and never type-checked; a parameter of the wrong type fails only at run time (hard cast throws, `as` yields `null`), so validate it at the start of the job
- Forced instant jobs (`ForceRunInstantJob` / `ForceRunScheduledJob`) bypass queue and concurrency protection
- A timed-out run is `Cancelled` and does not trigger dependencies; jobs must honor the `CancellationToken`
- After `Build()`, call `UseNCronJobAsync()` or `UseNCronJob()` whenever `RunAtStartup(...)` is used; see [Running Startup Jobs](features/startup-jobs.md)

## Repository sample map

- `sample/MinimalSample`: minimal recurring delegate job
- `sample/NCronJobSample`: typed jobs, notifications, retries, concurrency, dependencies, a startup job, instant job endpoints, and the dashboard at `/ncronjob`
- `sample/RunOnceSample`: startup job flow

## Preferred source of truth

When an example and prose disagree, prefer:

1. Public API and XML docs in `src/NCronJob`
2. The sample applications in `sample/*`
3. The feature pages in `docs/*`

## Machine-friendly documentation files

- [`llms.txt`](https://raw.githubusercontent.com/NCronJob-Dev/NCronJob/main/docs/llms.txt) provides a compact discovery index
- [llms-full.md](llms-full.md) provides a compact Markdown reference for retrieval and prompting
