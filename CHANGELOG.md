# Changelog

All notable changes to **NCronJob** will be documented in this file. The project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!-- The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) -->

## [Unreleased]

### Added

- Make IInstantJobRegistry accept job names. By [@nulltoken](https://github.com/nulltoken) in [#184](https://github.com/NCronJob-Dev/NCronJob/pull/215).

### Fixed

- Fix injection of context in dynamic jobs. By [@nulltoken](https://github.com/nulltoken) in [#184](https://github.com/NCronJob-Dev/NCronJob/pull/215).
- Teach `IRuntimeJobRegistry.RemoveJob()` to cope with disabled jobs. By [@nulltoken](https://github.com/nulltoken) in [#230](https://github.com/NCronJob-Dev/NCronJob/pull/230).

## [v4.3.4] - 2025-02-25

### Fixed

- Ensure orchestration includes dependents of faulted jobs. Added in [#195](https://github.com/NCronJob-Dev/NCronJob/issues/195), by [@nulltoken](https://github.com/nulltoken).
- Honor custom retry policies. Fixed in [#204](https://github.com/NCronJob-Dev/NCronJob/issues/204), by [@nulltoken](https://github.com/nulltoken).
- Detect ambiguous jobs triggered through `IInstantJobRegistry`. Fixed in [#213](https://github.com/NCronJob-Dev/NCronJob/issues/213), by [@nulltoken](https://github.com/nulltoken).

## [v4.3.3] - 2025-01-31

### Fixed

- Fixing minor concurrency issues. By [@nulltoken](https://github.com/nulltoken) in [#184](https://github.com/NCronJob-Dev/NCronJob/issues/184).

## [v4.3.2] - 2025-01-27

### Fixed

- Fixed an issue where exception handlers aren't called when a job can't be created. Reported by [@nulltoken](https://github.com/nulltoken) in [#177](https://github.com/NCronJob-Dev/NCronJob/issues/177). Fixed by [@linkdotnet](https://github.com/linkdotnet) in [#179](https://github.com/NCronJob-Dev/NCronJob/issues/179).

## [v4.3.1] - 2025-01-23

### Added

- Allow a cron job to also run at startup. Added in [#171](https://github.com/NCronJob-Dev/NCronJob/issues/171), by [@nulltoken](https://github.com/nulltoken).

- Teach startup jobs to optionaly prevent the application start on failure. Added in [#165](https://github.com/NCronJob-Dev/NCronJob/issues/165), by [@nulltoken](https://github.com/nulltoken).

### Fixed

- Prevent `RunAtStartup()` from blindly decorating all jobs of the same type. Added in [#170](https://github.com/NCronJob-Dev/NCronJob/issues/170), by [@nulltoken](https://github.com/nulltoken).
- Use UTC format for logger output.
- Prevented `ObjectDisposedException` in various places. Reported by [@nulltoken](https://github.com/nulltoken) in [#172](https://github.com/NCronJob-Dev/NCronJob/issues/172). Fixed by [@linkdotnet](https://github.com/linkdotnet).

## [v4.2.0] - 2025-01-17

### Added

- Expose an **experimental** basic job execution progress reporting hook. Added in [#157](https://github.com/NCronJob-Dev/NCronJob/issues/157), by [@nulltoken](https://github.com/nulltoken).

- Report additional `ExecutionState`s (`Cancelled` removed jobs and `Skipped` dependent jobs). Added in [#162](https://github.com/NCronJob-Dev/NCronJob/issues/162), by [@nulltoken](https://github.com/nulltoken).

## [v4.1.0] - 2025-01-02

### Added

- Expose typed version of `DisableJob()`. Added in [#151](https://github.com/NCronJob-Dev/NCronJob/issues/151), by [@nulltoken](https://github.com/nulltoken).
- Expose typed version of `EnableJob()`. Added in [#151](https://github.com/NCronJob-Dev/NCronJob/issues/151), by [@nulltoken](https://github.com/nulltoken).

### Changed

- Teach `IInstantJobRegistry` members to return the job correlation id. Changed in [#153](https://github.com/NCronJob-Dev/NCronJob/issues/153), by [@nulltoken](https://github.com/nulltoken).

### Fixed

- Make `RemoveJob<TJob>()` and `RemoveJob(Type)` remove all jobs of the given type. Fixed in [#151](https://github.com/NCronJob-Dev/NCronJob/issues/151), by [@nulltoken](https://github.com/nulltoken).
- Ensure `UpdateSchedule()` behavior is consistent. Fixed in [#151](https://github.com/NCronJob-Dev/NCronJob/issues/151), by [@nulltoken](https://github.com/nulltoken).

## [v4.0.2] - 2024-12-28

New `v4` release with some new features and improvements. Check the [`v4` migration guide](https://docs.ncronjob.dev/migration/v4/) for more information.

### Added

- Optionally make startup jobs run early

### Changed

- `IRuntimeRegistry.AddJob` is now called `TryRegister` to better reflect its behavior. 
- Explicit handling of duplicates
- Make `UseNCronJobAsync` mandatory when startup jobs have been defined 

## [v3.3.8] - 2024-11-16

### Changed

- Added `net9.0` release packages for hosting abstraction.

### Fixed

- Calling `AddJob` multiple times on `IRuntimeRegistry` triggered duplicates. Reported by [@vrohak](https://github.com/vrohak) in [#139](https://github.com/NCronJob-Dev/NCronJob/issues/139). Fixed by [@linkdotnet](https://github.com/linkdotnet)
- Calling `AddNCronJob` multiple times should register all jobs. Reported by [@skarum](https://github.com/skarum) in [#138](https://github.com/NCronJob-Dev/NCronJob/issues/138). Fixed by [@linkdotnet](https://github.com/linkdotnet)

## [v3.3.5] - 2024-11-04

### Changed

- Ensure parameter handling is coherent. Fixed in [#132](https://github.com/NCronJob-Dev/NCronJob/issues/132), by [@nulltoken](https://github.com/nulltoken).

- Prevent `InstantJobRegistry` from altering the content of `JobRegistry`. Fixed in [#131](https://github.com/NCronJob-Dev/NCronJob/issues/131), by [@nulltoken](https://github.com/nulltoken).

## [v3.3.4] - 2024-11-03

### Fixes

- Ensure multiples schedules of the same job with different chains of dependent jobs are properly processed. Identified in [#108](https://github.com/NCronJob-Dev/NCronJob/issues/108), by [@nulltoken](https://github.com/nulltoken).

- Teach `IRuntimeJobRegistry.RemoveJob()` to clean up potential dependent jobs. Fixes [#107](https://github.com/NCronJob-Dev/NCronJob/issues/107), by [@nulltoken](https://github.com/nulltoken).

## [v3.3.3] - 2024-10-31

### Changed

- Simplifications and refactoring of internal code. By [@nulltoken](https://github.com/nulltoken).

## [v3.3.2] - 2024-10-27

### Fixes

- `AddJob` during runtime didn't reevaluate the execution list correctly. Reported by [@IvaTutis](https://github.com/IvaTutis) in [#100](https://github.com/NCronJob-Dev/NCronJob/issues/100). Fixed by [@linkdotnet](https://github.com/linkdotnet).

### Added

- New `AddJob(Type jobType)` overload. By [@linkdotnet](https://github.com/linkdotnet).

## [v3.2.0] - 2024-10-19

### Fixed

- Fixed an issue where registering multiple dependent jobs would only call the last one. Reported by [@nulltoken](https://github.com/nulltoken) in [#101](https://github.com/NCronJob-Dev/NCronJob/issues/101). Fixed by [@linkdotnet](https://github.com/linkdotnet).

## [v3.1.3] - 2024-10-17

### Added

- Global exception handlers. By [@linkdotnet](https://github.com/linkdotnet). Reported by [@nulltoken](https://github.com/nulltoken).
- Expose `JobName` and `JobType` in `IJobExecutionContext`. 

## [v3.0.3] - 2024-09-15

This is a new major version! A bit of cleanup! Check the [`v3` migration guide](https://docs.ncronjob.dev/migration/v3/) for more information.

### Changed

- Created abstraction around `JobExecutionContext` to allow for easier testing and mocking. Will now be `IJobExecutionContext`.

### Removed

- Removed `enableSecondPrecision` as it is now inferred automatically.

## [v2.8.6] - 2024-08-29

### Fixed

- Dispose could still lead to issues when an exception was thrown inside a job/task.

## [v2.8.5] - 2024-08-18

### Fixed

- Disposing can lead to issues inside `StopAsync`. Fixed by [@linkdotnet](https://github.com/linkdotnet).

## [v2.8.4] - 2024-06-23

### Fixed

- Dependent jobs where registered as singleton instead of scoped. Fixed by [@linkdotnet](https://github.com/linkdotnet).

## [v2.8.3] - 2024-06-20

### Changed

- Identical Job Definitions will not lead to multiple instances of the job running concurrently. By [@linkdotnet](https://github.com/linkdotnet).

## [v2.8.2] - 2024-06-18

### Changed

- Instantiating `JobExecutionContext` is not obsolete to prevent build errors.

## [v2.8.1] - 2024-06-18

### Changed

- Removed preview feature as users had to opt-in to use it.

## [v2.8.0] - 2024-06-18

### Added

- Allow dynamically adding, removing jobs, updating the schedule or parameter and getting the schedule for a given job.
  Done by [@linkdotnet](https://github.com/linkdotnet). Reported by [@KorsG](https://github.com/KorsG) in [#83](https://github.com/NCronJob-Dev/NCronJob/issues/83)

### Changed

This release includes improvements to the way Jobs are scheduled included in PR [#85](https://github.com/NCronJob-Dev/NCronJob/pull/85) 
implemented by [@falvarez1](https://github.com/falvarez1)

- Improved scheduling accuracy by preventing jobs of different types from competing for the same queue.
- Long-running jobs of one type no longer affect other types, improving overall job scheduling and execution.
- Added notifications for job states and event hooks to enhance observability.
- Introduced queue notifications for better monitoring.
- Improved maintainability and concurrency handling.

### Fixed

- Closes [#65](https://github.com/NCronJob-Dev/NCronJob/issues/65)
- Fixes [#34](https://github.com/NCronJob-Dev/NCronJob/issues/34)

## [v2.7.4] - 2024-06-03

### Fixed

- Anonymous jobs don't execute multiple times

## [v2.7.3] - 2024-06-01

### Changed

- Don't depend on prerelease version in `net9.0`

## [v2.7.2] - 2024-06-01

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

## [v2.6.1] - 2024-05-25

This release has the same changes as `2.6.0` but fixes an issue in the release pipeline.

### Changed

- The NuGet.org "Release Notes" section will from now on be empty. It will contain a link to the releases.

## [v2.6.0] - 2024-05-25

### Changed

- API signature improvements - Enhanced the job scheduling framework with new classes and interfaces to better support job lifecycle management, including startup configuration and notification handling. This includes the introduction of `StartupStage<TJob>`, `NotificationStage<TJob>`, `IJobStage`, `IStartupStage<TJob>`, and `INotificationStage<TJob>`.
  [#70](https://github.com/NCronJob-Dev/NCronJob/pull/70) By [@falvarez1](https://github.com/falvarez1)

### Added

- Startup jobs - Run a job when the application starts. [#70](https://github.com/NCronJob-Dev/NCronJob/pull/70) By [@falvarez1](https://github.com/falvarez1)
- Sample project - Added a sample project to demonstrate Startup Jobs. [#70](https://github.com/NCronJob-Dev/NCronJob/pull/70) By [@falvarez1](https://github.com/falvarez1)

## [v2.5.0] - 2024-05-21

### Changed

- Instant jobs are executed with the highest priority.

### Fixed

- If an instant job is executed that is not registered, it will throw an exception instead of silently ignoring it.

## [v2.4.6] - 2024-05-21

### Fixed

- Retry/Concurrency Attributes where ignored

## [v2.4.5] - 2024-05-20

### Fixed

- Readded public constructor for `JobExeuctionContext` to make it unit testable

### Added

- Regex Indicator for cron expressions (IDE support)

## [v2.4.4] - 2024-05-20

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
  Fixed by [@linkdotnet](https://github.com/linkdotnet). Reported by [@KorsG](https://github.com/KorsG) in [#52](https://github.com/NCronJob-Dev/NCronJob/issues/52)

## [v2.3.2] - 2024-05-08

### Fixed

- Scheduler took local time instead of UTC as base. By [@falvarez1](https://github.com/falvarez1)

## [v2.3.1] - 2024-05-07

### Added

- TimeZone Support implemented by [@falvarez1](https://github.com/falvarez1) in PR [#22](https://github.com/NCronJob-Dev/NCronJob/pull/22).

## [v2.2.1] - 2024-05-05

### Added

- Ability to schedule a job to run after a specific delay.
- Ability to schedule a job to run at a specific time.

## [v2.1.4] - 2024-05-01

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

- Support for concurrent jobs and retries, as well as overall improvements, implemented by [@falvarez1](https://github.com/falvarez1) in PR [#21](https://github.com/NCronJob-Dev/NCronJob/pull/21).

## [v2.0.5] - 2024-04-19

### Changed

- Implementation of the scheduling. Better performance and closed some memory leaks
- Throw exception if job cannot be resolved with dependencies from the DI container. Reported and implemented by [@skarum](https://github.com/skarum) in [#23](https://github.com/NCronJob-Dev/NCronJob/issues/23)

## [v2.0.4] - 2024-04-16

### Fixed

- Don't log running jobs twice. Reported by [@ryanbuening](https://github.com/ryanbuening). Fixed by [@linkdotnet](https://github.com/linkdotnet)

## [v2.0.3] - 2024-04-15

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

## [v1.0.2] - 2024-04-11

### Changed

- Removed internal periodic timer so that instant jobs are really executed instantly and cron jobs on the correct time (rather than too late)

### Migration

- The `IsolationLevel` was completely removed as jobs are executed in their own scope anyway. It behaves like `Isolation.NewTask` by default.
- `TimerInterval` in `NCronJobOptions` was removed as it no longer used.

## [v0.13.2] - 2024-03-29

### Changed

- Smaller performance improvements

## [v0.13.1] - 2024-03-25

### Changed

- Check if `IsolationLevel` is in a valid range - otherwise it throws an exception
- Small refactorings

## [v0.13.0] - 2024-03-23

### Changed

- Moved `EnableSecondPrecision` from `AddNCronJob` to `AddCronJob` to allow for more granular control over the precision of the cron expression
- When a job is not registered, a error is logged, but the execution of other jobs is not interrupted

## [v0.12.0] - 2024-03-22

### Changed

- Breaking Change: `Run` is now called `RunAsync` to reflect the asynchronous nature of the method
- `Run` doesn't take an optional `CancellationToken` anymore, as this is passed in anyway.

## [v0.11.5] - 2024-03-22

## [v0.11.4] - 2024-03-21

### Added

- Ability to set cron expressions with second-level precision
- Support for `net9.0`
- Support for Isolation Level to run jobs independent of the current scheduler
- Notification system that allows to run a task when a job is finished

## [v0.10.1] - 2024-03-19

### Changed

- Every Job-Run has its own scope

## [v0.10.0] - 2024-03-18

### Added

- Ability to set the timer interval

### Changed

- `AddCronJob` registers as scoped service instead of transient

## [v0.9.3] - 2024-03-18

### Changed

- `AddCronJob` registers the job as transient instead of singleton

## [v0.9.2] - 2024-03-17

### Changed

- Simplified much of the logic for scheduling

### Fixed

- Instant jobs weren't executed correctly

## [v0.9.1] - 2024-03-17

### Changed

- Fixed some docs

## [v0.9.0] - 2024-03-17

### Added

- Initial Release of **NCronJob** with lots of features
- The ability to schedule jobs using a cron expression
- The ability to instantly run a job
- Parameterized jobs - instant as well as cron jobs!
- Integrated in ASP.NET - Access your DI container like you would in any other service

[unreleased]: https://github.com/NCronJob-Dev/NCronJob/compare/v4.3.4...HEAD
[v4.3.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v4.3.3...v4.3.4
[v4.3.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v4.3.2...v4.3.3
[v4.3.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v4.3.1...v4.3.2
[v4.3.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v4.2.0...v4.3.1
[v4.2.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v4.1.0...v4.2.0
[v4.1.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v4.0.2...v4.1.0
[v4.0.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.3.8...v4.0.2
[v3.3.8]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.3.5...v3.3.8
[v3.3.5]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.3.4...v3.3.5
[v3.3.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.3.3...v3.3.4
[v3.3.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.3.2...v3.3.3
[v3.3.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.2.0...v3.3.2
[v3.2.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.1.3...v3.2.0
[v3.1.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v3.0.3...v3.1.3
[v3.0.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.8.6...v3.0.3
[v2.8.6]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.8.5...v2.8.6
[v2.8.5]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.8.4...v2.8.5
[v2.8.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.8.3...v2.8.4
[v2.8.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.8.2...v2.8.3
[v2.8.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.8.1...v2.8.2
[v2.8.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.8.0...v2.8.1
[v2.8.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.7.4...v2.8.0
[v2.7.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.7.3...v2.7.4
[v2.7.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.7.2...v2.7.3
[v2.7.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.6.1...v2.7.2
[v2.6.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.6.0...v2.6.1
[v2.6.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.5.0...v2.6.0
[v2.5.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.4.6...v2.5.0
[v2.4.6]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.4.5...v2.4.6
[v2.4.5]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.4.4...v2.4.5
[v2.4.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.3.2...v2.4.4
[v2.3.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.3.1...v2.3.2
[v2.3.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.2.1...v2.3.1
[v2.2.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.1.4...v2.2.1
[v2.1.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.0.5...v2.1.4
[v2.0.5]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.0.4...v2.0.5
[v2.0.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v2.0.3...v2.0.4
[v2.0.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v1.0.2...v2.0.3
[v1.0.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.13.2...v1.0.2
[v0.13.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.13.1...v0.13.2
[v0.13.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.13.0...v0.13.1
[v0.13.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.12.0...v0.13.0
[v0.12.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.11.5...v0.12.0
[v0.11.5]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.11.4...v0.11.5
[v0.11.4]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.10.1...v0.11.4
[v0.10.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.10.0...v0.10.1
[v0.10.0]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.9.3...v0.10.0
[v0.9.3]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.9.2...v0.9.3
[v0.9.2]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.9.1...v0.9.2
[v0.9.1]: https://github.com/NCronJob-Dev/NCronJob/compare/v0.9.0...v0.9.1
[v0.9.0]: https://github.com/NCronJob-Dev/NCronJob/compare/cf7df8ffb3a740fa63ccc439336b42b890c9519c...v0.9.0
