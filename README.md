<h1 align="center">NCronJob</h1>

<p align="center">
  <img src="/assets/logo_small.png" alt="logo" width="120px" height="120px"/>
  <br>
  <em>Scheduling made easy</em>
  <br>
</p>

# 📣ANNOUNCEMENT📣
The library has moved from `LinkDotNet.NCronJob` to just `NCronJob`!
Please uninstall the old package and install the new one. Learn more about it here: https://github.com/NCronJob-Dev/NCronJob/discussions/66

[![.NET](https://github.com/NCronJob-Dev/NCronJob/actions/workflows/dotnet.yml/badge.svg)](https://github.com/NCronJob-Dev/NCronJob/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/dt/NCronJob.svg)](https://www.nuget.org/packages/NCronJob)
[![NuGet](https://img.shields.io/nuget/vpre/NCronJob.svg)](https://www.nuget.org/packages/NCronJob)

# NCronJob

A Job Scheduler sitting on top of `IHostedService` in .NET.

Often, one finds oneself between the simplicity of `BackgroundService`/`IHostedService` and the complexity of
a full-blown scheduler like `Hangfire` or `Quartz`.
This library aims to fill that gap by providing a simple and easy-to-use job scheduler that can be used in any .NET
application and feels "native".

There's no need to set up a database—just schedule your tasks right away! The library provides two ways of scheduling
jobs:

1. Instant jobs - Run a job immediately (or with a small delay, or at a specific date and time).
2. Cron jobs - Schedule a job using a cron expression.

The whole documentation can be found here: [NCronJob Documentation](https://ncronjob-dev.github.io/NCronJob/)

- [📣ANNOUNCEMENT📣](#announcement)
- [NCronJob](#ncronjob)
  - [Features](#features)
  - [Not features](#not-features)
  - [Short example](#short-example)
    - [Minimal Job API](#minimal-job-api)
    - [Via the `IJob` interface](#via-the-ijob-interface)
  - [Triggering an instant job](#triggering-an-instant-job)
  - [Running a Job at Startup](#running-a-job-at-startup)
  - [Defining Job Dependencies](#defining-job-dependencies)
  - [Support \& Contributing](#support--contributing)


## Features

- [x] The ability to schedule jobs using a cron expression.
- [x] The ability to instantly run a job.
- [x] Parameterized jobs - Instant as well as cron jobs!
- [x] Integration with ASP.NET - Access your DI container like you would in any other service.
- [x] Get notified when a job is done (either successfully or with an error).
- [x] Retries - If a job fails, it will be retried.
- [x] The job scheduler supports TimeZones. Defaults to UTC time.
- [x] Minimal API for Jobs - Implement jobs in a one-liner.
- [x] Startup jobs - Run a job when the application starts.
- [x] Define job dependencies - trigger another job if one was successful or faulted!

## Not features

As this is a simple scheduler, some features are not included by design. If you need these features, you might want to
look into a more advanced scheduler like `Hangfire` or `Quartz`.

- [ ] Job persistence - Jobs are not persisted between restarts of the application.
- [ ] Job history - There is no history of jobs that have been run.
- [ ] Progress state - There is no way to track the progress of a job. The library supports notifying when a job is
  done, but not the progress of the job itself.

## Short example

There are two ways to define a job.

### Minimal Job API

You can use this library in a simple one-liner:
```csharp
builder.Services.AddNCronJob((ILoggerFactory factory, TimeProvider timeProvider) =>
{
    var logger = factory.CreateLogger("My Anonymous Job");
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
}, "*/5 * * * * *");
```

With this simple lambda, you can define a job that runs every 5 seconds. Pass in all dependencies, just like you would with a Minimal API.

### Via the `IJob` interface

1. Import the namespace (or let your IDE do the dirty work)

```csharp
using NCronJob;
```

2. Create a job

```csharp
public class PrintHelloWorld : IJob
{
    private readonly ILogger<PrintHelloWorld> logger;

    public PrintHelloWorld(ILogger<PrintHelloWorld> logger)
    {
        this.logger = logger;
    }

    public Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation("Hello World");
        logger.LogInformation("Parameter: {Parameter}", context.Parameter);

        return Task.CompletedTask;
    }
}
```

3. Register the NCronJob and the job in your `Program.cs`

```csharp
builder.Services.AddNCronJob(options =>
    options.AddJob<PrintHelloWorld>(j => 
    {
        // Every minute and optional parameter
        j.WithCronExpression("* * * * *")
         .WithParameter("Hello World");
    }));
```

4. Run your application and see the magic happen!

## Triggering an instant job

If the need arises and you want to trigger a job instantly, you can do so:

```csharp
public class MyService
{
  private readonly IInstantJobRegistry jobRegistry;
  
  public MyService(IInstantJobRegistry jobRegistry) => this.jobRegistry = jobRegistry;

  public void MyMethod() => jobRegistry.RunInstantJob<MyJob>("I am an optional parameter");
    
  // Alternatively, you can also run an anonymous job
  public void MyOtherMethod() => jobRegistry.RunInstantJob((MyOtherService service) => service.Do());
}
```

## Running a Job at Startup

If you want a job to run when the application starts, you can configure it to run at startup using the `RunAtStartup` method. Here is an example:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<MyJob>()
           .RunAtStartup();
});
```

In this example, the job of type 'MyJob' will be executed as soon as the application starts. This is
useful for tasks that need to run immediately upon application startup, such as initial data loading or cleanup tasks.

## Defining Job Dependencies

First you need to import data and then transform it? Well, but how do you make sure that the data is imported before you transform it? Sure, you could just give a delay, but what if the import takes longer than expected? This is where job dependencies come in handy!

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<ImportData>(p => p.WithCronExpression("0 0 * * *")
     .ExecuteWhen(
        success: s => s.RunJob<TransformData>("Optional Parameter"),
        faulted: s => s.RunJob<Notify>("Another Optional Parameter"));
});
```

You just want to trigger a service and don't want to define a whole new job? No problem! The Minimal API is available here as well:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<ImportData>(p => p.WithCronExpression("0 0 * * *")
     .ExecuteWhen(
        success: s => s.RunJob(async (ITransformer transformer) => await transformer.TransformDataAsync()),
        faulted: s => s.RunJob(async (INotificationService notifier) => await notifier.NotifyAsync())
});
```

## Support & Contributing

Thanks to all [contributors](https://github.com/NCronJob-Dev/NCronJob/graphs/contributors) and people that are creating
bug-reports and valuable input:

<a href="https://github.com/NCronJob-Dev/NCronJob/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=NCronJob-Dev/NCronJob" alt="Supporters" />
</a>
