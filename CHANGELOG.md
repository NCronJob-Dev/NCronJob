# Changelog

All notable changes to **NCronJob** will be documented in this file. The project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!-- The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) -->

## [Unreleased]

### Added
- Allow dynamically adding, removing jobs, updating the schedule or parameter and getting the schedule for a given job.
  Done by [@linkdotnet](https://github.com/linkdotnet). Reported by [@KorsG](https://github.com/KorsG) in [#83](https://github.com/linkdotnet/NCronJob/issues/83)

## [2.7.4] - 2024-06-03

### Fixed

- Anonymous jobs don't execute multiple times

## [2.7.3] - 2024-06-01

### Changed

- Don't depend on prerelease version in `net9.0`

## [2.7.2] - 2024-06-01

### Added

- Ability to add a timezone for a "minimal job".
- Run jobs automatically when a job either succeeded or failed allowing to model a job pipeline. By [@linkdotnet](https://github.com/linkdotnet).

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<ImportData>(p => p.WithCronExpression("0 0 * * *")
     .ExecuteWhen(
        success: s => s.RunJob<TransformData>("Optional Parameter"),
        faulted: s => s.RunJob<Notify>("Another Optional Parameter"));
});
```

- Minimal API for instant jobs and job dependencies. By [@linkdotnet](https://github.com/linkdotnet).

```csharp
public void MyOtherMethod() => jobRegistry.RunInstantJob((MyOtherService service) => service.Do());
```

### Changed

- Replace `Microsoft.Extensions.Hosting` with `Microsoft.Extensions.Hosting.Abstractions` for better compatibility. Reported by [@chrisls121](https://github.com/chrisls121) in [#74](https://github.com/NCronJob-Dev/NCronJob/issues/74). Implemented by [@linkdotnet](https://github.com/linkdotnet).

## [2.6.1] - 2024-05-25

This release has the same changes as `2.6.0` but fixes an issue in the release pipeline.

### Changed

- The NuGet.org "Release Notes" section will from now on be empty. It will contain a link to the releases.

## [2.6.0] - 2024-05-25

### Changed

- API signature improvements - Enhanced the job scheduling framework with new classes and interfaces to better support job lifecycle management, including startup configuration and notification handling. This includes the introduction of `StartupStage<TJob>`, `NotificationStage<TJob>`, `IJobStage`, `IStartupStage<TJob>`, and `INotificationStage<TJob>`.
  (#70) By [@falvarez1](https://github.com/falvarez1)

### Added

- Startup jobs - Run a job when the application starts. (#70) By [@falvarez1](https://github.com/falvarez1)
- Sample project - Added a sample project to demonstrate Startup Jobs. (#70) By [@falvarez1](https://github.com/falvarez1)

## [2.5.0] - 2024-05-21

### Changed

- Instant jobs are executed with the highest priority.

### Fixed

- If an instant job is executed that is not registered, it will throw an exception instead of silently ignoring it.

## [2.4.6] - 2024-05-21

### Fixed

- Retry/Concurrency Attributes where ignored

## [2.4.5] - 2024-05-20

### Fixed

- Readded public constructor for `JobExeuctionContext` to make it unit testable

### Added

- Regex Indicator for cron expressions (IDE support)

## [2.4.4] - 2024-05-20

### Added

- New minimal API to register jobs. By [@falvarez1](https://github.com/falvarez1)
  Jobs can be defined via a simple lambda:

```csharp
builder.Services.AddNCronJob((ILoggerFactory factory, TimeProvider timeProvider) =>
{
    var logger = factory.CreateLogger("My Job");
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
}, "*/5 * * * * *");
```

### Fixed

- Instant jobs did ignore the concurrency attribute and global concurrency settings.
  Fixed by [@linkdotnet](https://github.com/linkdotnet). Reported by [@KorsG](https://github.com/KorsG) in [#52](https://github.com/linkdotnet/NCronJob/issues/52)

## [2.3.2] - 2024-05-08

### Fixed

- Scheduler took local time instead of UTC as base. By [@falvarez1](https://github.com/falvarez1)

## [2.3.1] - 2024-05-07

### Added

- TimeZone Support implemented by [@falvarez1](https://github.com/falvarez1) in PR [#22](https://github.com/linkdotnet/NCronJob/pull/22).

## [2.2.1] - 2024-05-05

### Added

- Ability to schedule a job to run after a specific delay.
- Ability to schedule a job to run at a specific time.

## [2.1.4] - 2024-05-01

### Added

- **Retry Mechanism:** Improved robustness of job executions, especially in cases of transient failures. Includes exponential backoff and fixed interval strategies.
- **Concurrency Control:** Introduced a `SupportsConcurrency` attribute to provide fine-grained control over the concurrency behavior of individual jobs. This attribute allows specifying the maximum degree of parallelism.
- **New WaitForJobsOrTimeout:** Added an enhanced version of `WaitForJobsOrTimeout` to the Tests project that allows time advancement for each run, providing more precise control over test execution timing.
- **Cancellation Support:** Jobs now support graceful cancellation during execution and SIGTERM.

### Changed

- **Concurrency Management:** Implemented improved global concurrency handling techniques within `CronScheduler`. This change enhances flexibility and scalability in managing job executions.
- **Async Job Executions:** Job executions are now fully async from the moment the scheduler triggers the job, ensuring better performance and resource utilization.

### Fixed

- **Test Framework Bugs:** Addressed specific bugs in the testing framework, ensuring that all tests are now passing and provide reliable results.
- CRON jobs that are scheduled more than 50 days in the future did throw an exception.

### Contributors

- Support for concurrent jobs and retries, as well as overall improvements, implemented by [@falvarez1](https://github.com/falvarez1) in PR [#21](https://github.com/linkdotnet/NCronJob/pull/21).

## [2.0.5] - 2024-04-19

### Changed

- Implementation of the scheduling. Better performance and closed some memory leaks
- Throw exception if job cannot be resolved with dependencies from the DI container. Reported and implemented by [@skarum](https://github.com/skarum) in [#23](https://github.com/linkdotnet/NCronJob/issues/23)

## [2.0.4] - 2024-04-16

### Fixed

- Don't log running jobs twice. Reported by [@ryanbuening](https://github.com/ryanbuening). Fixed by [@linkdotnet](https://github.com/linkdotnet)

## [2.0.3] - 2024-04-15

With `v2` the overall API was cleaned up and made more consistent. `AddNCronJob` and `AddCronJob` are merged into one service defintion:

### Added

- The IDE can help with RegEx pattern thanks to the `StringSyntaxAttribute`.
- Added more code documentation (xmldoc) with many examples to rely less on the README.

### Changed

- In `v1` one would define as such:

```csharp
services.AddNCronJob();
services.AddCronJob<PrintHelloWorld>(options => 
{
    options.CronExpression = "* * * * *";
    options.Parameter = "Hello World";
});
```

With `v2` the `CronExpression` is moved towards the builder pattern and `AddCronJob` is merged into `AddNCronJob`:

```csharp
services.AddNCronJob(options => 
{
    options.AddJob<PrintHelloWorld>(j => 
    {
        j.WithCronExpression("* * * * *")
         .WithParameter("Hello World");
    });
});
```

- Cleaned up `AddNCronJob` to not accidentally build the service container

## [1.0.2] - 2024-04-11

### Changed

- Removed internal periodic timer so that instant jobs are really executed instantly and cron jobs on the correct time (rather than too late)

### Migration

- The `IsolationLevel` was completely removed as jobs are executed in their own scope anyway. It behaves like `Isolation.NewTask` by default.
- `TimerInterval` in `NCronJobOptions` was removed as it no longer used.

## [0.13.2] - 2024-03-29

### Changed

- Smaller performance improvements

## [0.13.1] - 2024-03-25

### Changed

- Check if `IsolationLevel` is in a valid range - otherwise it throws an exception
- Small refactorings

## [0.13.0] - 2024-03-23

### Changed

- Moved `EnableSecondPrecision` from `AddNCronJob` to `AddCronJob` to allow for more granular control over the precision of the cron expression
- When a job is not registered, a error is logged, but the execution of other jobs is not interrupted

## [0.12.0] - 2024-03-22

### Changed

- Breaking Change: `Run` is now called `RunAsync` to reflect the asynchronous nature of the method
- `Run` doesn't take an optional `CancellationToken` anymore, as this is passed in anyway.

## [0.11.5] - 2024-03-22

## [0.11.4] - 2024-03-21

### Added

- Ability to set cron expressions with second-level precision
- Support for `net9.0`
- Support for Isolation Level to run jobs independent of the current scheduler
- Notification system that allows to run a task when a job is finished

## [0.10.1] - 2024-03-19

### Changed

- Every Job-Run has its own scope

## [0.10.0] - 2024-03-18

### Added

- Ability to set the timer interval

### Changed

- `AddCronJob` registers as scoped service instead of transient

## [0.9.3] - 2024-03-18

### Changed

- `AddCronJob` registers the job as transient instead of singleton

## [0.9.2] - 2024-03-17

### Changed

- Simplified much of the logic for scheduling

### Fixed

- Instant jobs weren't executed correctly

## [0.9.1] - 2024-03-17

### Changed

- Fixed some docs

## [0.9.0] - 2024-03-17

### Added

- Initial Release of **NCronJob** with lots of features
- The ability to schedule jobs using a cron expression
- The ability to instantly run a job
- Parameterized jobs - instant as well as cron jobs!
- Integrated in ASP.NET - Access your DI container like you would in any other service

[unreleased]: https://github.com/NCronJob-Dev/NCronJob/compare/2.7.4...HEAD
[2.7.4]: https://github.com/NCronJob-Dev/NCronJob/compare/2.7.3...2.7.4
[2.7.3]: https://github.com/NCronJob-Dev/NCronJob/compare/2.7.2...2.7.3
[2.7.2]: https://github.com/NCronJob-Dev/NCronJob/compare/2.6.1...2.7.2
[2.6.1]: https://github.com/NCronJob-Dev/NCronJob/compare/2.6.0...2.6.1
[2.6.0]: https://github.com/NCronJob-Dev/NCronJob/compare/2.5.0...2.6.0
[2.5.0]: https://github.com/NCronJob-Dev/NCronJob/compare/2.4.6...2.5.0
[2.4.6]: https://github.com/linkdotnet/NCronJob/compare/2.4.5...2.4.6
[2.4.5]: https://github.com/linkdotnet/NCronJob/compare/2.4.4...2.4.5
[2.4.4]: https://github.com/linkdotnet/NCronJob/compare/2.3.2...2.4.4
[2.3.2]: https://github.com/linkdotnet/NCronJob/compare/2.3.1...2.3.2
[2.3.1]: https://github.com/linkdotnet/NCronJob/compare/2.2.1...2.3.1
[2.2.1]: https://github.com/linkdotnet/NCronJob/compare/2.1.4...2.2.1
[2.1.4]: https://github.com/linkdotnet/NCronJob/compare/2.0.5...2.1.4
[2.0.5]: https://github.com/linkdotnet/NCronJob/compare/2.0.4...2.0.5
[2.0.4]: https://github.com/linkdotnet/NCronJob/compare/2.0.3...2.0.4
[2.0.3]: https://github.com/linkdotnet/NCronJob/compare/1.0.2...2.0.3
[1.0.2]: https://github.com/linkdotnet/NCronJob/compare/0.13.2...1.0.2
[0.13.2]: https://github.com/linkdotnet/NCronJob/compare/0.13.1...0.13.2
[0.13.1]: https://github.com/linkdotnet/NCronJob/compare/0.13.0...0.13.1
[0.13.0]: https://github.com/linkdotnet/NCronJob/compare/0.12.0...0.13.0
[0.12.0]: https://github.com/linkdotnet/NCronJob/compare/0.11.5...0.12.0
[0.11.5]: https://github.com/linkdotnet/NCronJob/compare/0.11.4...0.11.5
[0.11.4]: https://github.com/linkdotnet/NCronJob/compare/0.10.1...0.11.4
[0.10.1]: https://github.com/linkdotnet/NCronJob/compare/0.10.0...0.10.1
[0.10.0]: https://github.com/linkdotnet/NCronJob/compare/0.9.3...0.10.0
[0.9.3]: https://github.com/linkdotnet/NCronJob/compare/0.9.2...0.9.3
[0.9.2]: https://github.com/linkdotnet/NCronJob/compare/0.9.1...0.9.2
[0.9.1]: https://github.com/linkdotnet/NCronJob/compare/0.9.0...0.9.1
[0.9.0]: https://github.com/linkdotnet/NCronJob/compare/cf7df8ffb3a740fa63ccc439336b42b890c9519c...0.9.0
