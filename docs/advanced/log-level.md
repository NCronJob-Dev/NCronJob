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

## Log scopes

Every job run is executed within a [log scope](https://learn.microsoft.com/dotnet/core/extensions/logging#log-scopes). All log entries written during the run carry the following properties, including the ones written by your job through its injected `ILogger<T>`:

| Property | Description |
|---|---|
| `JobName` | The name of the job. If a custom name was given, it is followed by the job type in parentheses. |
| `JobRunId` | The unique identifier of this run. |
| `CorrelationId` | The identifier shared by a job and all of its dependent jobs (see `IJobExecutionContext.CorrelationId`). |
| `TriggerType` | How the run was started: `Cron`, `Instant`, `Startup` or `Dependent`. |

This makes it possible to filter or group all log entries of a single run, or of a whole chain of dependent jobs, without passing any context around manually.

Structured logging providers (for example Serilog or OpenTelemetry) pick up the properties automatically. The console logger only shows scopes when they are enabled:

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
