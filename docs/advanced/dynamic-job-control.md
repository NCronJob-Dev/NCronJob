# Dynamic Job Control
**NCronJob** allows you to dynmically add or remove CRON jobs from the scheduler. This is useful when you want to add jobs at runtime or remove jobs based on some condition without restarting the scheduler.

## Defining job names
The core idea is to define an unique job name for each job that might be mutated during runtime. The job name is an optional parameter:

```csharp
builder.Services.AddNCronJob(builder => 
{
    builder.AddJob<SampleJob>(p => p.WithCronExpression("* * * * *").WithName("MyName"));
});
```

The same applies to Minimal API:

```csharp
builder.Services.AddNCronJob(b => b.AddJob(() => {}, "* * * * *", "MyName"));
```

### Job Names

Every recurring job (CRON job) can have a unique job name that identifies the job. Job names have to be unique across all jobs that were given a name. If a job is added without a name, it will not be possible to remove or update the job at runtime by name.

## Adding jobs
To add a job at runtime, leverage the `IRuntimeJobRegistry` interface:

```csharp
app.MapPost("/add-job", (IRuntimeJobRegistry registry) => 
{
    var hasSucceeded = registry.TryRegister(n => n.AddJob<SampleJob>(p => p.WithCronExpression("* * * * *").WithName("MyName")), out Exception? exc);
    
    if (!hasSucceeded)
    {
        return TypedResults.Error(exc?.Message);
    }

    return TypedResults.Ok();
});
```

The outer `AddJob` accepts a builder just like `builder.Services.AddNCronJob` does. The inner `AddJob` is the one that actually adds the job to the scheduler and does behave exactly the same.

## Removing jobs
There are two ways to remove a job from the scheduler. By name or by type. To remove a job by name:

```csharp
app.MapDelete("/remove-job", (IRuntimeJobRegistry registry) => 
{
    registry.RemoveJob("MyName");
    return TypedResults.Ok();
});
```

That will remove one job from the scheduler that has the name `MyName`. In contrast removing by type will remove all jobs of the given type (so zero to many jobs):

```csharp
app.MapDelete("/remove-job", (IRuntimeJobRegistry registry) => 
{
    registry.RemoveJob<SampleJob>(); // Alternatively RemoveJob(typeof(SampleJob))
    return TypedResults.Ok();
});
```

## Updating the job schedule
Updating the job schedule is done via the `UpdateSchedule` method. This method accepts a job name, a new CRON expression and optionally the time zone:

```csharp
app.MapPut("/update-job", (IRuntimeJobRegistry registry) => 
{
    registry.UpdateSchedule("MyName", "* * * * *", TimeZoneInfo.Utc);
    return TypedResults.Ok();
});
```

Updating a schedule will lead to the job being rescheduled with the new CRON expression. Any planned job with the "old" schedule will be cancelled and rescheduled with the new schedule.

## Updating the parameter
Updating the parameter of a job is done via the `UpdateParameter` method. This method accepts a job name and a new parameter:

```csharp
app.MapPut("/update-job", (IRuntimeJobRegistry registry) => 
{
    registry.UpdateParameter("MyName", new MyParameter());
    return TypedResults.Ok();
});
```

Updating a parameter will lead to the job being rescheduled with the new parameter. Any planned job with the "old" parameter will be cancelled and rescheduled with the new parameter.

## Retrieving a job schedule by name
To retrieve the schedule of a job by name, use the `TryGetSchedule` method:

```csharp
var found = registry.TryGetSchedule("MyName", out string? cronExpression, out TimeZoneInfo? timeZone);
```

The cron expression and time zone can be `null` even if the job was found. This indicates that the job has no schedule (like dependent jobs).

## Disabling and enabling jobs
There are two ways to disable a job from the scheduler. By name or by type.

To disable a job by name:

```csharp
app.MapPut("/disable-job", (IRuntimeJobRegistry registry) => 
{
    registry.DisableJob("MyName");
    return TypedResults.Ok();
});
```

That will prevent one job named `MyName` from being scheduled.

In contrast disabling by type will disable all jobs of the given type (so zero to many jobs):

```csharp
app.MapPut("/disable-job", (IRuntimeJobRegistry registry) =>
{
    registry.DisableJob<SampleJob>(); // Alternatively DisableJob(typeof(SampleJob))
    return TypedResults.Ok();
});
```

If a job is disabled, it will not be scheduled anymore. Any planned job will be cancelled and the job will be removed from the scheduler.

To enable a job, use the `EnableJob` method:

```csharp
app.MapPut("/enable-job", (IRuntimeJobRegistry registry) => 
{
    registry.EnableJob("MyName");
    return TypedResults.Ok();
});
```
