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

1. Instant jobs - just run a job right away
2. Cron jobs - schedule a job using a cron expression

## Features

- [x] The ability to schedule jobs using a cron expression
- [x] The ability to instantly run a job
- [x] Parameterized jobs - instant as well as cron jobs!
- [x] Integrated in ASP.NET - Access your DI container like you would in any other service
- [x] Get notified when a job is done (either successfully or with an error).

## Not features

As this is a simple scheduler, some features are not included by design. If you need these features, you might want to
look into a more advanced scheduler like `Hangfire` or `Quartz`.

- [ ] Job persistence - Jobs are not persisted between restarts of the application.
- [ ] Job history - There is no history of jobs that have been run.
- [ ] Retries - If a job fails, it is not retried.
- [ ] Progress state - There is no way to track the progress of a job. The library will support notifying when a job is
  done, but not the progress of the job itself.
- [ ] The job scheduler always uses UTC time. We might change that in the future.

## Short example

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
builder.Services.AddNCronJob();
builder.Services.AddCronJob<PrintHelloWorld>(options => 
{
    // Every minute and optional parameter
    options.WithCronExpression("* * * * *")
           .WithParameter("Hello World");
});
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

## Getting notified when a job is done

**NCronJob** provides a way to get notified when a job is done. For this, implement a `IJobNotificationHandler<TJob>`
and register it in your DI container.

```csharp
Services.AddNotificationHandler<MyJobNotificationHandler, MyJob>();
```

This allows to run logic after a job is done. The `JobExecutionContext` and the `Exception` (if there was one) are
passed to the `Handle` method.

```csharp
public class MyJobNotificationHandler : IJobNotificationHandler<MyJob>
{
    private readonly ILogger<MyJobNotificationHandler> logger;

    public MyJobNotificationHandler(ILogger<MyJobNotificationHandler> logger)
    {
        this.logger = logger;
    }

    public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken token)
    {
        if (exception is not null)
        {
            logger.LogError(exception, "Job failed");
        }
        else
        {
            logger.LogInformation("Job was successful");
            logger.LogInformation("Output: {Output}", context.Output);
        }

        return Task.CompletedTask;
    }
}
```

## Advanced Cases

### Scheduling multiple schedules for the same job

If you want to schedule a job multiple times, you can do so by calling utilizing the builder:

```csharp
AddCronJob<PrintHelloWorld>(options => 
{
    options.WithCronExpression("0 * * * *")
           .WithParameter("Hello World")
           .And
           .WithCronExpression("45 * * * *")
           .WithParameter("Hello World");
});

```

### Log Level

The **NCronJob** scheduler can be configured to log at a specific log level.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "LinkDotNet.NCronJob": "Debug"
```

## Migration from `v1` to `v2`
Version 2 of **NCronJob** brings some breaking changes to mae a better API.

### `CronExpression` moved towards builder
In `v1` one would define as such:
```csharp
services.AddCronJob<PrintHelloWorld>(options => 
{
    options.CronExpression = "* * * * *";
    options.Parameter = "Hello World";
});
```

With `v2` the `CronExpression` is moved towards the builder pattern:
```csharp
services.AddCronJob<PrintHelloWorld>(options => 
{
    options.WithCronExpression("* * * * *")
           .WithParameter("Hello World");
});
```

This allows to easily define multiple jobs without adding mich boilerplate code.
```csharp
services.AddCronJob<PrintHelloWorld>(options => 
{
    options.WithCronExpression("0 * * * *").WithParameter("Foo");
           .WithCronExpression("0 0 * * *").WithParmeter("Bar");
});
```

## Support & Contributing

Thanks to all [contributors](https://github.com/linkdotnet/NCronJob/graphs/contributors) and people that are creating
bug-reports and valuable input:

<a href="https://github.com/linkdotnet/NCronJob/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=linkdotnet/NCronJob" alt="Supporters" />
</a>
