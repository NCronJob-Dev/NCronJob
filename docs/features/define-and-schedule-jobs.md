# Define and schedule jobs

## Understanding `IJob`
In **NCronJob**, jobs are defined by implementing the `IJob` interface. This interface contains a single method, `RunAsync`, where you define the task's execution logic.

NCronJob registers `IJob` implementations as **scoped** services within your application's dependency injection container. This means that a new scope is created for each job execution, ensuring isolation and allowing for clean dependency management (particularly important when working with frameworks like Entity Framework Core).

## Defining a Job
Follow these steps to create and schedule a job in NCronJob:

```csharp
public class MyCronJob : IJob 
{
    private readonly ILogger<MyCronJob> _logger;

    public MyCronJob(ILogger<MyCronJob> logger)
    {
        // MyCronJob lives in the container so you can inject services here
        _logger = logger;
    }

    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        _logger.LogInformation("MyCronJob is executing!");
        
        // Add your job logic here (e.g., database updates, sending emails, etc.)

        return Task.CompletedTask;
    }
}
```

### Registering the Job
```csharp
using NCronJob;

// Inside your service configuration
Services.AddNCronJob(options => 
{
    options.AddJob<MyCronJob>(j => 
    {
        j.WithCronExpression("*/5 * * * *"); //  Runs every 5 minutes
    });
});
```

## Chaining Cron Expressions with `And`

Execute the same job on multiple schedules using the `And` command:

```csharp
Services.AddNCronJob(options => 
{
    options.AddJob<MyCronJob>(j => 
    {
        j.WithCronExpression("0 8 * * *")  // Every day at 8:00 AM
         .And
         .WithCronExpression("0 20 * * *"); // Every day at 8:00 PM 
    });
});
```

!!! info
    
    Defining multiple identifical schedules for the same job will not lead to multiple instances of the job running concurrently. NCronJob will ensure that only one instance of the job is running at any given time. One can define different custom names for each schedule to differentiate between them. See [*"Defining job names"*](../advanced/dynamic-job-control.md#defining-job-names).

The following example illustrates how to define multiple schedules that are identical and will only lead to one instance of the job running at any given time:

```csharp
Services.AddNCronJob(options => 
{
    options.AddJob<MyCronJob>(j => 
    {
        j.WithCronExpression("0 20 * * *")
         .And
         .WithCronExpression("0 20 * * *");
    });
});
```

## Scheduling Jobs With Time Zones
The library offers you the ability to schedule jobs using time zones.

```csharp
Services.AddNCronJob(options => 
{
    var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); 
    options.AddJob<MyCronJob>(j => 
    {
        j.WithCronExpression("0 15 * * *", timeZoneInfo: timeZone); // Every day at 3:00 PM PST
    });
});
```
