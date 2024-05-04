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

This library is possible because of these wonderful people:

<a href="https://github.com/linkdotnet/NCronJob/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=linkdotnet/NCronJob" alt="Supporters" />
</a>

If you want to support this project, you can:

 * [Leave a star ⭐️](https://github.com/linkdotnet/NCronJob)
 * If you find issues, report them to us: [https://github.com/linkdotnet/NCronJob/issues](https://github.com/linkdotnet/NCronJob/issues)
 * If you have a feature request, let us know: [https://github.com/linkdotnet/NCronJob/issues](https://github.com/linkdotnet/NCronJob/issues)
