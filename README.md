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
- [x] Retries - If a job fails, it will be retried.
- [x] The job scheduler supports TimeZones. Defaults to UTC time.

## Not features

As this is a simple scheduler, some features are not included by design. If you need these features, you might want to
look into a more advanced scheduler like `Hangfire` or `Quartz`.

- [ ] Job persistence - Jobs are not persisted between restarts of the application.
- [ ] Job history - There is no history of jobs that have been run.
- [ ] Progress state - There is no way to track the progress of a job. The library will support notifying when a job is
  done, but not the progress of the job itself.

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

## Getting notified when a job is done

**NCronJob** provides a way to get notified when a job is done. For this, implement a `IJobNotificationHandler<TJob>`
and register it in your DI container.

```csharp
builder.Services.AddNCronJob(options =>
    options.AddCronJob<PrintHelloWorld>(j => 
    {
        // Every minute and optional parameter
        j.WithCronExpression("* * * * *")
         .WithParameter("Hello World");
    })
    .AddNotificationHandler<MyJobNotificationHandler, PrintHelloWorld>());
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
Services.AddNCronJob(options =>
    options.AddJob<PrintHelloWorld>(j => 
    {
        j.WithCronExpression("* * * * *")
         .WithParameter("Hello World")
         .And
         .WithCronExpression("0 * * * *")
         .WithParameter("Hello World Again");
    }));
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
Services.AddNCronJob(options => 
{
    options.AddJob<PrintHelloWorld>(j => 
    {
        j.WithCronExpression("* * * * *")
         .WithParameter("Hello World");
    });
});
```

This allows to easily define multiple jobs without adding much boilerplate code.
```csharp
Services.AddNCronJob(options => 
{
    options.AddJob<PrintHelloWorld>(p => p
        .WithCronExpression("0 * * * *").WithParameter("Foo")
        .And
        .WithCronExpression("0 0 * * *").WithParameter("Bar"));
});
```

## Retry Support

The new Retry support provides a robust mechanism for handling transient failures by retrying failed operations. This feature is implemented using the `RetryPolicy` attribute that can be applied to any class implementing the `IJob` interface.

### How It Works

The `RetryPolicy` attribute allows you to specify the number of retry attempts and the strategy for handling retries. There are two built-in retry strategies:
- **ExponentialBackoff:** Increases the delay between retry attempts exponentially.
- **FixedInterval:** Keeps the delay between retry attempts consistent.

### Using Retry Policies

Here are examples of how to use the built-in retry policies:

#### Example 1: Basic Retry Policy, defaults to Exponential Backoff

```csharp
[RetryPolicy(retryCount: 4)]
public class RetryJob(ILogger<RetryJob> logger) : IJob
{
    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        var attemptCount = context.Attempts;

        if (attemptCount <= 3)
        {
            logger.LogWarning("RetryJob simulating failure.");
            throw new InvalidOperationException("Simulated operation failure in RetryJob.");
        }

        logger.LogInformation($"RetryJob with Id {context.Id} was attempted {attemptCount} times.");
        await Task.CompletedTask;
    }
}
```

#### Example 2: Fixed Interval

```csharp
[RetryPolicy(4, PolicyType.FixedInterval)]
public class FixedIntervalRetryJob(ILogger<FixedIntervalRetryJob> logger) : IJob
{
    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        var attemptCount = context.Attempts;

        if (attemptCount <= 3)
        {
            logger.LogWarning("FixedIntervalRetryJob simulating failure.");
            throw new InvalidOperationException("Simulated operation failure in FixedIntervalRetryJob.");
        }

        logger.LogInformation($"FixedIntervalRetryJob with Id {context.Id} was attempted {attemptCount} times.");
        await Task.CompletedTask;
    }
}
```

### Advanced: Custom Retry Policies

You can also create custom retry policies by implementing the `IPolicyCreator` interface. This allows you to define complex retry logic tailored to your specific needs.

```csharp
[RetryPolicy<MyCustomPolicyCreator>(retryCount:4, delayFactor:1)]
public class CustomPolicyJob(ILogger<CustomPolicyJob> logger) : IJob
{
    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        var attemptCount = context.Attempts;

        if (attemptCount <= 3)
        {
            logger.LogWarning("FixedIntervalRetryJob simulating failure.");
            throw new InvalidOperationException("Simulated operation failure in FixedIntervalRetryJob.");
        }

        logger.LogInformation($"CustomPolicyJob with Id {context.Id} was attempted {attemptCount} times.");
        await Task.CompletedTask;
    }
}

public class MyCustomPolicyCreator : IPolicyCreator
{
    public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2)
    {
        return Policy.Handle<Exception>()
            .WaitAndRetryAsync(maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(delayFactor, retryAttempt)));
    }
}
```

## Concurrency Support

Concurrency support allows multiple instances of the same job type to run simultaneously, controlled by the `SupportsConcurrency` attribute. This feature is crucial for efficiently managing jobs that are capable of running in parallel without interference.

### How It Works

The `SupportsConcurrency` attribute specifies the maximum degree of parallelism for job instances. This means you can define how many instances of a particular job can run concurrently, optimizing performance and resource utilization based on the nature of the job and the system capabilities.

### Using the SupportsConcurrency Attribute

Here is an example of how to apply this attribute to a job:

#### Example: Concurrency in Jobs

```csharp
[SupportsConcurrency(10)]
public class ConcurrentJob : IJob
{
    private readonly ILogger<ConcurrentJob> logger;

    public ConcurrentJob(ILogger<ConcurrentJob> logger)
    {
        this.logger = logger;
    }

    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation($"ConcurrentJob with Id {context.Id} is running.");
        // Simulate some work by delaying
        await Task.Delay(5000, token);
        logger.LogInformation($"ConcurrentJob with Id {context.Id} has completed.");
    }
}
```

### Important Considerations

#### Ensuring Job Idempotency
When using concurrency, it's essential to ensure that each job instance is idempotent. This means that even if the job is executed multiple times concurrently or sequentially, the outcome and side effects should remain consistent, without unintended duplication or conflict.

#### Resource Allocation Caution
Jobs that are marked to support concurrency should be designed carefully to avoid contention over shared resources. This includes, but is not limited to, database connections, file handles, or any external systems. In scenarios where shared resources are unavoidable, proper synchronization mechanisms or concurrency control techniques, such as semaphores, mutexes, or transactional control, should be implemented to prevent race conditions and ensure data integrity.


## Support & Contributing

Thanks to all [contributors](https://github.com/linkdotnet/NCronJob/graphs/contributors) and people that are creating
bug-reports and valuable input:

<a href="https://github.com/linkdotnet/NCronJob/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=linkdotnet/NCronJob" alt="Supporters" />
</a>
