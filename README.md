<h1 align="center">NCronJob</h1>

<p align="center">
  <img src="assets/logo_small.png" alt="logo" width="120px" height="120px"/>
  <br>
  <em>Scheduling made easy</em>
  <br>
</p>

[![.NET](https://github.com/linkdotnet/NCronJob/actions/workflows/dotnet.yml/badge.svg)](https://github.com/linkdotnet/NCronJob/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/dt/LinkDotNet.NCronJob.svg)](https://www.nuget.org/packages/LinkDotNet.NCronJob)
[![NuGet](https://img.shields.io/nuget/vpre/LinkDotNet.NCronJob.svg)](https://www.nuget.org/packages/LinkDotNet.NCronJob)

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

The whole documentation can be found here: [NCronJob Documentation](https://linkdotnet.github.io/NCronJob/)

- [NCronJob](#ncronjob)
  - [Features](#features)
  - [Not features](#not-features)
  - [Short example](#short-example)
  - [Triggering an instant job](#triggering-an-instant-job)
  - [Support \& Contributing](#support--contributing)


## Features

- [x] The ability to schedule jobs using a cron expression
- [x] The ability to instantly run a job
- [x] Parameterized jobs - instant as well as cron jobs!
- [x] Integrated in ASP.NET - Access your DI container like you would in any other service
- [x] Get notified when a job is done (either successfully or with an error).
- [x] Retries - If a job fails, it will be retried.
- [x] The job scheduler supports TimeZones. Defaults to UTC time.
- [x] Minimal API for Jobs - implement jobs in an oneliner

## Not features

As this is a simple scheduler, some features are not included by design. If you need these features, you might want to
look into a more advanced scheduler like `Hangfire` or `Quartz`.

- [ ] Job persistence - Jobs are not persisted between restarts of the application.
- [ ] Job history - There is no history of jobs that have been run.
- [ ] Progress state - There is no way to track the progress of a job. The library will support notifying when a job is
  done, but not the progress of the job itself.

## Short example

There are two ways of defining a job.

### Minimal Job API
You can use this library in a simple one-liner:
```csharp
builder.Services.AddNCronJob((ILogger<Program> logger, TimeProvider timeProvider) =>
{
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
}, "*/5 * * * * *");
```

With this simple lambda, you can define a job that runs every 5 seconds. Pass in all dependencies, just like you would with a Minimal API.

### Via the `IJob` interface

1. Import the namespace (or let your IDE do the dirty work)

```csharp
using LinkDotNet.NCronJob;
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

4. Run your application and see the magic happen

## Triggering an instant job

If the need arises and you want to trigger a job instantly, you can do so:

```csharp
public class MyService
{
  private readonly IInstantJobRegistry jobRegistry;
  
  public MyService(IInstantJobRegistry jobRegistry) => this.jobRegistry = jobRegistry;

  public void MyMethod() => jobRegistry.RunInstantJob<MyJob>("I am an optional parameter");
}
```

## Support & Contributing

Thanks to all [contributors](https://github.com/linkdotnet/NCronJob/graphs/contributors) and people that are creating
bug-reports and valuable input:

<a href="https://github.com/linkdotnet/NCronJob/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=linkdotnet/NCronJob" alt="Supporters" />
</a>
