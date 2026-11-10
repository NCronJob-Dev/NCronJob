# Passing parameters to a job

Often a job needs some kind of configuration or parameter to run. Imagine you have a job that generates a report and can run daily, weekly or monthly. You could create three different jobs for each frequency, but that would be a lot of duplicated code. Instead, you can pass in parameters to the job.

```csharp
Services.AddNCronJob(options =>
{
    options.AddJob<ReportJob>(j =>
    {
        // Runs every day at midnight and passes in the string "daily"
        j.WithCronExpression("0 0 * * *").WithParameter("daily")
         .And
         .WithCronExpression("0 0 * * 0").WithParameter("weekly")
         .And
         .WithCronExpression("0 0 1 * *").WithParameter("monthly");
    });
});
```

A job that accepts a parameter implements `IJob<TParameter>`. The parameter is available, typed, via `context.Parameter`:

```csharp
public class ReportJob : IJob<string>
{
    public Task RunAsync(IJobExecutionContext<string> context, CancellationToken token)
    {
        switch (context.Parameter)
        {
            case "daily":
                // Generate daily report
                break;
            case "weekly":
                // Generate weekly report
                break;
            case "monthly":
                // Generate monthly report
                break;
        }
        return Task.CompletedTask;
    }
}
```

A job that implements the plain `IJob` doesn't accept a parameter.

It's also possible to configure a job with only a parameter and no cron expression.

```csharp
Services.AddNCronJob(options =>
{
    options.AddJob<MaintenanceJob>(j =>
    {
        j.WithParameter("lightMode");
    });
});
```

This can later be triggered through the `IInstantJobRegistry` in a preconfigured mode (See [_"Instant jobs"_](./instant-jobs.md)).

```csharp
app.MapPost("/maintenance/light", (IInstantJobRegistry jobRegistry) =>
{
    // "lightMode" will be passed as a parameter to the job
    jobRegistry.RunInstantJob<MaintenanceJob>();
    return Results.Ok();
});
```

Of course, the preconfigured parameter can also be overriden when triggering the job.

```csharp
app.MapPost("/maintenance/thorough", (IInstantJobRegistry jobRegistry) =>
{
    jobRegistry.RunInstantJob<MaintenanceJob>("thoroughMode");
    return Results.Ok();
});
```

## Parameters are type-checked

The parameter of a job must be assignable to its `TParameter`. Every API that sets a parameter checks it and rejects a wrong one with an `ArgumentException` right away:

- `WithParameter`, when the job is registered. `IRuntimeJobRegistry.TryRegister` doesn't throw: it returns `false` and hands out the `ArgumentException` through its `out` parameter
- `RunJob<TJob>(parameter)` for dependent jobs, when the dependency is registered
- `IRuntimeJobRegistry.UpdateParameter`
- `RunInstantJob`, `RunScheduledJob`, `ForceRunInstantJob` and `ForceRunScheduledJob`, when the job is triggered
- the [dashboard](dashboard.md), which deserializes the JSON into `TParameter`

```csharp
public sealed record ReportRequest(string Region);

public class ReportJob : IJob<ReportRequest>
{
    public Task RunAsync(IJobExecutionContext<ReportRequest> context, CancellationToken token)
    {
        var region = context.Parameter.Region;
        return Task.CompletedTask;
    }
}

jobRegistry.RunInstantJob<ReportJob>(new ReportRequest("EU")); // runs
jobRegistry.RunInstantJob<ReportJob>("weekly");                  // throws ArgumentException
```

Passing a parameter to a job that implements the plain `IJob` throws as well.

The compiler can't check the parameter, because these APIs accept `object?`. The check runs when the call is made, not when the job runs.

### Optional parameters

A run without a parameter gets `default(TParameter)`: `null` for reference types, `0` for an `int`. Declare the parameter as nullable when it is optional, for example `IJob<ReportRequest?>`, and handle `null` in the job.

### Accepting any parameter

`IJob<object?>` accepts every parameter, like an `IJob` did before v5. Prefer a dedicated type, for example a `record`, so that wrong parameters are rejected.

### Delegate jobs

A delegate job declares its parameter type through an `IJobExecutionContext<TParameter>` argument:

```csharp
builder.Services.AddNCronJob(
    (IJobExecutionContext<ReportRequest?> context) => Console.WriteLine(context.Parameter?.Region ?? "all regions"),
    "0 0 * * *");
```

### Notification handlers and exception handlers

Handlers get an `IJobExecutionContext`. For a job that implements `IJob<TParameter>`, it is an `IJobExecutionContext<TParameter>`:

```csharp
public class ReportJobHandler : IJobNotificationHandler<ReportJob>
{
    public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
    {
        if (context is IJobExecutionContext<ReportRequest> reportContext)
        {
            // reportContext.Parameter.Region
        }
        return Task.CompletedTask;
    }
}
```

## Parameters are not immutable

Passed in parameters are not immutable by default or cloned throughout the job execution. This means that if you change the parameter in the job, it will also change in the next execution. If you need to keep the parameter unchanged, you should clone it in the job.

```csharp
public class MyParameter
{
  public int Counter { get; set; }
}

Services.AddNCronJob(b =>
{
  b.AddJob<MyJob>(p => p.WithCronExpression("0 0 * * *"));
});

public class MyJob : IJob<MyParameter>
{
  public Task RunAsync(IJobExecutionContext<MyParameter> context, CancellationToken token)
  {
     var myParam = context.Parameter;
     myParam.Counter++; // This will be incremented with each job run
     return Task.CompletedTask;
  }
}
```

If `MyJob` runs twice already and is invoked a third time, `myParam.Counter` will be 2 when the function gets invoked.
