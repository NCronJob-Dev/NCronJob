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

## Practical rules

- Name jobs when you need runtime management
- Name jobs when the same job type is registered more than once
- Use `RunInstantJob("name")` to avoid ambiguity with duplicate job types
- Parameters are passed by reference and are not serialized
- Forced instant and scheduled jobs bypass queueing and concurrency safeguards
- Startup jobs require `UseNCronJobAsync()` or `UseNCronJob()`

## Best starting points

- First setup: [Getting Started](getting-started.md)
- Docs guide: [Documentation Map](documentation-map.md)
- Agents: [Agent & Automation Guide](agent-guide.md)

## Feature index

- [Define and Schedule Jobs](features/define-and-schedule-jobs.md)
- [Passing Parameters](features/parameters.md)
- [Triggering instant jobs](features/instant-jobs.md)
- [Concurrency control](features/concurrency-control.md)
- [Job timeouts and run expiry](features/timeouts-and-expiry.md)
- [Retry support](features/retry-support.md)
- [Minimal API](features/minimal-api.md)
- [Running Startup Jobs](features/startup-jobs.md)
- [Model Dependencies](features/model-dependencies.md)
- [Notifications](features/notifications.md)
- [Conditional Job Scheduling](features/conditional-job-scheduling.md)
- [Dynamic Job Control](advanced/dynamic-job-control.md)
- [Known gotchas](advanced/known-gotchas.md)

## Sample projects

- `sample/MinimalSample`
- `sample/NCronJobSample`
- `sample/RunOnceSample`
