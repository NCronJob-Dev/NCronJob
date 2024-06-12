# Dynamic Job Control
**NCronJob** allows you to dynmically add or remove CRON jobs from the scheduler. This is useful when you want to add jobs at runtime or remove jobs based on some condition without restarting the scheduler.

## Defining job names
The core idea is to define an unique job name for each job that might be mutated during runtime. The job name is an optional parameter:

```csharp
builder.Services.AddNCronJob(builder => 
{
    builder.AddJob<SampleJob>(p => p.WithCronExpression("* * * * *").WithName("MyName"));
});
```

The same applies to Minimal API:

```csharp
builder.Services.AddNCronJob(b => b.AddJob(() => {}, "* * * * *", "MyName));
```

## Adding jobs
To add a job at runtime, leverage the `IJobRegistry` interface:

```csharp
app.MapPost("/add-job", (IJobRegistry registry) => 
{
    registry.AddJob(n => n.AddJob<SampleJob>(p => p.WithCronExpression("* * * * *").WithName("MyName")));
    return TypedResults.Ok();
});
```

The outer `AddJob` accepts a builder just like `builder.Services.AddNCronJob` does. The inner `AddJob` is the one that actually adds the job to the scheduler and does behave exactly the same.

## Removing jobs
There are two ways to remove a job from the scheduler. By name or by type. To remove a job by name:

```csharp
app.MapDelete("/remove-job", (IJobRegistry registry) => 
{
    registry.RemoveJob("MyName");
    return TypedResults.Ok();
});
```

That will remove one job from the scheduler that has the name `MyName`. In contrast removing by type will remove all jobs of the given type (so zero to many jobs):

```csharp
app.MapDelete("/remove-job", (IJobRegistry registry) => 
{
    registry.RemoveJob<SampleJob>();
    return TypedResults.Ok();
});
```
