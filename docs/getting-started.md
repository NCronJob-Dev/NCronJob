# Getting Started

Use **NCronJob** when you want recurring or on-demand background work without introducing a database-backed scheduler.

## 1. Install the package

[![NuGet](https://img.shields.io/nuget/vpre/NCronJob.svg)](https://www.nuget.org/packages/NCronJob)

```bash
dotnet add package NCronJob
```

If you prefer editing the project file directly, copy the current version from [NuGet](https://www.nuget.org/packages/NCronJob).

## 2. Choose a starting style

NCronJob supports two main authoring styles:

- **Minimal job API** for small jobs and quick setup
- **`IJob` implementations** when you want reusable job types, notification handlers, or richer configuration

If you want a working sample first, start with:

- [`sample/MinimalSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/MinimalSample) for the minimal API
- [`sample/NCronJobSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/NCronJobSample) for typed jobs, notifications, retries, and instant jobs
- [`sample/RunOnceSample`](https://github.com/NCronJob-Dev/NCronJob/tree/main/sample/RunOnceSample) for startup jobs

The generic-host examples below assume an ASP.NET app or a host-based project such as `dotnet new worker`. If you start from a plain console app, add a reference to `Microsoft.Extensions.Hosting` first.

## 3. Minimal job API quick start

This is the smallest useful setup:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCronJob;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNCronJob((ILogger<Program> logger) =>
{
    if (logger.IsEnabled(LogLevel.Information))
        logger.LogInformation("Hello World from NCronJob.");
}, "*/5 * * * * *");

await builder.Build().RunAsync();
```

Use this style when you want to keep the job close to your application bootstrap and resolve dependencies directly from DI.

## 4. Typed job quick start

Use a typed job when you want a named class, richer composition, or related handlers.

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCronJob;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNCronJob(options =>
    options.AddJob<PrintHelloWorld>(job =>
        job.WithCronExpression("* * * * *")
           .WithParameter("Hello World")));

await builder.Build().RunAsync();

public sealed class PrintHelloWorld(ILogger<PrintHelloWorld> logger) : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation("Hello World");
        logger.LogInformation("Parameter: {Parameter}", context.Parameter);
        return Task.CompletedTask;
    }
}
```

## 5. Understand when `UseNCronJobAsync` is required

You only need `UseNCronJobAsync` or `UseNCronJob` when you register **startup jobs** via `RunAtStartup(...)`.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using NCronJob;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNCronJob(options =>
    options.AddJob<WarmupJob>(job => job.RunAtStartup()));

var app = builder.Build();

await app.UseNCronJobAsync();
await app.RunAsync();

public sealed class WarmupJob(ILogger<WarmupJob> logger) : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation("Startup warmup finished.");
        return Task.CompletedTask;
    }
}
```

After `Build()`, call `UseNCronJobAsync()` or `UseNCronJob()` whenever you use `RunAtStartup(...)`. Regular recurring jobs and instant jobs do not require it. See [Running Startup Jobs](features/startup-jobs.md) for the full flow.

## 6. Know the next building blocks

Once the first job is running, the next pages most users need are:

- [Define and Schedule Jobs](features/define-and-schedule-jobs.md)
- [Passing Parameters](features/parameters.md)
- [Triggering instant jobs](features/instant-jobs.md)
- [Concurrency control](features/concurrency-control.md)
- [Job timeouts and run expiry](features/timeouts-and-expiry.md)
- [Retry support](features/retry-support.md)
- [Running Startup Jobs](features/startup-jobs.md)
- [Dynamic Job Control](advanced/dynamic-job-control.md)

## 7. Need a documentation map?

Use the [Documentation Map](documentation-map.md) for a guided tour of the docs, samples, and recommended reading order.

If you are using an assistant or automation tool, also see the [Agent & Automation Guide](agent-guide.md).
