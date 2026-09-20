# NCronJob

A Job Scheduler sitting on top of `IHostedService` in dotnet.

Often times one finds themself between the simplicity of the `BackgroundService`/`IHostedService` and the complexity of
a full-blown `Hangfire` or `Quartz` scheduler.
This library aims to fill that gap by providing a simple and easy to use job scheduler that can be used in any dotnet
application and feels "native".

So no need for setting up a database, just schedule your stuff right away! The library gives you two ways of scheduling
jobs:

1. Instant jobs - just run a job right away (or with a small delay; or with a given date and time)
2. Cron jobs - schedule a job using a cron expression

---

## Start with the right guide

- New user: [Getting Started](getting-started.md)
- Need a guided overview: [Documentation Map](documentation-map.md)
- Using a coding assistant or automation: [Agent & Automation Guide](agent-guide.md)

## What to read next

- [Define and Schedule Jobs](features/define-and-schedule-jobs.md)
- [Triggering instant jobs](features/instant-jobs.md)
- [Concurrency control](features/concurrency-control.md)
- [Dynamic Job Control](advanced/dynamic-job-control.md)
- [Known gotchas](advanced/known-gotchas.md)

## Sample applications

- [`sample/MinimalSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/MinimalSample)
- [`sample/NCronJobSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/NCronJobSample)
- [`sample/RunOnceSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/RunOnceSample)

## Agent-friendly entry points

- [`llms.txt`](https://raw.githubusercontent.com/NCronJob-Dev/NCronJob/main/docs/llms.txt)
- [Compact Markdown Reference](llms-full.md)

This library is possible because of these wonderful people:

<a href="https://github.com/NCronJob-Dev/NCronJob/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=NCronJob-Dev/NCronJob" alt="Supporters" />
</a>

If you want to support this project, you can:

 * [Leave a star ⭐️](https://github.com/NCronJob-Dev/NCronJob)
 * If you find issues, report them to us: [https://github.com/NCronJob-Dev/NCronJob/issues](https://github.com/NCronJob-Dev/NCronJob/issues)
 * If you have a feature request, let us know: [https://github.com/NCronJob-Dev/NCronJob/issues](https://github.com/NCronJob-Dev/NCronJob/issues)
