# Controlling the log-level

The **NCronJob** scheduler can be configured to log at a specific log level.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "NCronJob": "Debug"
    }
  }
}
```

## Startup summary

At startup, NCronJob logs one `Information` entry listing all cron jobs with their schedule, time zone and next run. This answers whether a job is scheduled at all:

```
NCronJob started with 2 jobs:
  nightly-import   0 2 * * *   Europe/Helsinki next 2026-11-07 02:00
  cache-warmup     */5 * * * * UTC             next 2026-11-06 19:10
```

Disabled jobs show `disabled` instead of the next run. Adding and removing jobs from the internal queue is logged at `Debug`.

## Log scopes

Every job run is executed within a [log scope](https://learn.microsoft.com/dotnet/core/extensions/logging#log-scopes). All log entries written during the run carry the following properties, including the ones written by your job through its injected `ILogger<T>`:

| Property | Description |
|---|---|
| `JobName` | The job type (for example `MyApp.PrintHelloWorldJob`). If a custom name was given, it comes first and the type follows in parentheses. For delegate (minimal API) jobs, the type part is `Untyped job <name>`, or a generated identifier when no name was given. |
| `JobRunId` | The unique identifier of this run. |
| `CorrelationId` | The identifier shared by a job and all of its dependent jobs (see `IJobExecutionContext.CorrelationId`). |
| `TriggerType` | How the run was started: `Cron`, `Instant`, `Startup` or `Dependent`. |

This makes it possible to filter or group all log entries of a single run, or of a whole chain of dependent jobs, without passing any context around manually.

Whether scopes end up in your logs depends on the logging provider and its configuration. Most structured logging providers can turn scopes into properties (for example Serilog via `Serilog.Extensions.Logging`, or OpenTelemetry with `IncludeScopes` enabled). The console logger only shows scopes when they are enabled:

```json
{
  "Logging": {
    "Console": {
      "FormatterName": "simple",
      "FormatterOptions": {
        "IncludeScopes": true
      }
    }
  }
}
```

With scopes enabled, a log entry of a job looks like this:

```
info: MyApp.PrintHelloWorldJob[0]
      => Job MyApp.PrintHelloWorldJob run 3f0c2a5e-... (correlation id 9b1d7c44-..., triggered by Cron)
      Hello World
```
