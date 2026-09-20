# Documentation Map

Use this page to decide where to go next based on the way you plan to use **NCronJob**.

## Start here

- [Getting Started](getting-started.md) for the first working setup
- [Define and Schedule Jobs](features/define-and-schedule-jobs.md) for recurring jobs
- [Minimal API](features/minimal-api.md) for delegate-based jobs

## Common user journeys

### I want a recurring background job

1. [Getting Started](getting-started.md)
2. [Define and Schedule Jobs](features/define-and-schedule-jobs.md)
3. [Passing Parameters](features/parameters.md)
4. [Concurrency control](features/concurrency-control.md)
5. [Job timeouts and run expiry](features/timeouts-and-expiry.md)

### I want to trigger work from an HTTP endpoint or application code

1. [Triggering instant jobs](features/instant-jobs.md)
2. [Passing Parameters](features/parameters.md)
3. [Notifications](features/notifications.md)
4. [Dynamic Job Control](advanced/dynamic-job-control.md)

### I want orchestration between jobs

1. [Model Dependencies](features/model-dependencies.md)
2. [Conditional Job Scheduling](features/conditional-job-scheduling.md)
3. [Notifications](features/notifications.md)

### I want to change jobs at runtime

1. [Dynamic Job Control](advanced/dynamic-job-control.md)
2. [Define and Schedule Jobs](features/define-and-schedule-jobs.md)
3. [Known gotchas](advanced/known-gotchas.md)

## Sample applications

The repository contains three useful starting points:

- [`sample/MinimalSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/MinimalSample): smallest recurring job setup
- [`sample/NCronJobSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/NCronJobSample): typed jobs, retries, concurrency, notifications, and HTTP-triggered instant jobs
- [`sample/RunOnceSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/RunOnceSample): startup jobs via `RunAtStartup(...)` and `UseNCronJobAsync()`

## Important concepts to keep in mind

- Jobs run in-process and are **not persisted** across restarts
- Parameters are passed by reference and are **not serialized**
- Named jobs are the best choice when you want runtime management or to avoid ambiguity
- Forced instant jobs bypass queue and concurrency safeguards
- Startup jobs need `UseNCronJobAsync()` or `UseNCronJob()`

## Human docs vs. agent docs

Most pages in this site are written for human readers.

If you are consuming the docs with an agent or want a compact machine-friendly entry point, use:

- [Agent & Automation Guide](agent-guide.md)
- [`llms.txt`](https://raw.githubusercontent.com/NCronJob-Dev/NCronJob/main/docs/llms.txt)
- [Compact Markdown Reference](llms-full.md)
