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

### Runtime interaction

- `IInstantJobRegistry` triggers instant and delayed jobs
- `IRuntimeJobRegistry` adds, removes, enables, disables, and updates named recurring jobs at runtime

## Recommended reading order for agents

1. [Getting Started](getting-started.md)
2. [Define and Schedule Jobs](features/define-and-schedule-jobs.md)
3. [Triggering instant jobs](features/instant-jobs.md)
4. [Dynamic Job Control](advanced/dynamic-job-control.md)
5. [Known gotchas](advanced/known-gotchas.md)

## High-signal behavioral notes

- Use **named jobs** when the same job type is registered multiple times or when runtime management is needed
- `RunInstantJob<TJob>()` can become ambiguous if multiple registrations exist for the same job type; prefer `RunInstantJob("job-name")` in that case
- Parameters are passed by reference, not serialized, so mutation after enqueueing can affect execution
- Forced instant jobs (`ForceRunInstantJob` / `ForceRunScheduledJob`) bypass queue and concurrency protection
- Startup jobs fail fast during host startup if `UseNCronJobAsync()` or `UseNCronJob()` is not called after registering `RunAtStartup(...)`; see [Running Startup Jobs](features/startup-jobs.md)

## Repository sample map

- `sample/MinimalSample`: minimal recurring delegate job
- `sample/NCronJobSample`: typed jobs, notifications, retries, concurrency, instant job endpoints
- `sample/RunOnceSample`: startup job flow

## Preferred source of truth

When an example and prose disagree, prefer:

1. Public API and XML docs in `src/NCronJob`
2. The sample applications in `sample/*`
3. The feature pages in `docs/*`

## Machine-friendly documentation files

- [`llms.txt`](https://docs.ncronjob.dev/llms.txt) provides a compact discovery index
- [llms-full.md](llms-full.md) provides a compact Markdown reference for retrieval and prompting
