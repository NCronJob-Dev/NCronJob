# Changelog

All notable changes to **NCronJob** will be documented in this file. The project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!-- The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) -->

## [Unreleased]

### Changed

- Moved `EnableSecondPrecision` from `AddNCronJob` to `AddCronJob` to allow for more granular control over the precision of the cron expression

## [0.12.0] - 2024-03-22

### Changed

-   Breaking Change: `Run` is now called `RunAsync` to reflect the asynchronous nature of the method
-   `Run` doesn't take an optional `CancellationToken` anymore, as this is passed in anyway.

## [0.11.5] - 2024-03-22

## [0.11.4] - 2024-03-21

### Added

-   Ability to set cron expressions with second-level precision
-   Support for `net9.0`
-   Support for Isolation Level to run jobs independent of the current scheduler
-   Notification system that allows to run a task when a job is finished

## [0.10.1] - 2024-03-19

### Changed

-   Every Job-Run has its own scope

## [0.10.0] - 2024-03-18

### Added

-   Ability to set the timer interval

### Changed

-   `AddCronJob` registers as scoped service instead of transient

## [0.9.3] - 2024-03-18

### Changed

-   `AddCronJob` registers the job as transient instead of singleton

## [0.9.2] - 2024-03-17

### Changed

-   Simplified much of the logic for scheduling

### Fixed

-   Instant jobs weren't executed correctly

## [0.9.1] - 2024-03-17

### Changed

-   Fixed some docs

## [0.9.0] - 2024-03-17

### Added

-   Initial Release of **NCronJob** with lots of features
-   The ability to schedule jobs using a cron expression
-   The ability to instantly run a job
-   Parameterized jobs - instant as well as cron jobs!
-   Integrated in ASP.NET - Access your DI container like you would in any other service

[Unreleased]: https://github.com/linkdotnet/NCronJob/compare/0.12.0...HEAD

[0.12.0]: https://github.com/linkdotnet/NCronJob/compare/0.11.5...0.12.0

[0.11.5]: https://github.com/linkdotnet/NCronJob/compare/0.11.4...0.11.5

[0.11.4]: https://github.com/linkdotnet/NCronJob/compare/0.10.1...0.11.4

[0.10.1]: https://github.com/linkdotnet/NCronJob/compare/0.10.0...0.10.1

[0.10.0]: https://github.com/linkdotnet/NCronJob/compare/0.9.3...0.10.0

[0.9.3]: https://github.com/linkdotnet/NCronJob/compare/0.9.2...0.9.3

[0.9.2]: https://github.com/linkdotnet/NCronJob/compare/0.9.1...0.9.2

[0.9.1]: https://github.com/linkdotnet/NCronJob/compare/0.9.0...0.9.1

[0.9.0]: https://github.com/linkdotnet/NCronJob/compare/cf7df8ffb3a740fa63ccc439336b42b890c9519c...0.9.0
