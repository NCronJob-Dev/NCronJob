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

- First setup: [Getting Started](https://docs.ncronjob.dev/getting-started/)
- Docs guide: [Documentation Map](https://docs.ncronjob.dev/documentation-map/)
- Agents: [Agent & Automation Guide](https://docs.ncronjob.dev/agent-guide/)

## Feature index

- [Define and Schedule Jobs](https://docs.ncronjob.dev/features/define-and-schedule-jobs/)
- [Passing Parameters](https://docs.ncronjob.dev/features/parameters/)
- [Triggering instant jobs](https://docs.ncronjob.dev/features/instant-jobs/)
- [Concurrency control](https://docs.ncronjob.dev/features/concurrency-control/)
- [Job timeouts and run expiry](https://docs.ncronjob.dev/features/timeouts-and-expiry/)
- [Retry support](https://docs.ncronjob.dev/features/retry-support/)
- [Minimal API](https://docs.ncronjob.dev/features/minimal-api/)
- [Running Startup Jobs](https://docs.ncronjob.dev/features/startup-jobs/)
- [Model Dependencies](https://docs.ncronjob.dev/features/model-dependencies/)
- [Notifications](https://docs.ncronjob.dev/features/notifications/)
- [Conditional Job Scheduling](https://docs.ncronjob.dev/features/conditional-job-scheduling/)
- [Dynamic Job Control](https://docs.ncronjob.dev/advanced/dynamic-job-control/)
- [Known gotchas](https://docs.ncronjob.dev/advanced/known-gotchas/)

## Sample projects

- `sample/MinimalSample` — https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/MinimalSample
- `sample/NCronJobSample` — https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/NCronJobSample
- `sample/RunOnceSample` — https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/RunOnceSample
