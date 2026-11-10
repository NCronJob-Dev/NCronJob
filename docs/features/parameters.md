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

In the `ReportJob` you can now access the parameter via the `JobExecutionContext`:

```csharp
public class ReportJob : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        var parameter = context.Parameter;
        // Do something with the parameter
        switch (parameter)
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

## Parameters are not type-checked

`context.Parameter` is of type `object?`. Every API that sets a parameter accepts any object: `WithParameter`, `RunInstantJob`, `RunScheduledJob`, `IRuntimeJobRegistry.UpdateParameter`, `RunJob<TJob>(parameter)` for dependent jobs, and the [dashboard](dashboard.md). NCronJob does not check that a parameter matches what the job expects, neither when the job is registered nor when it is triggered.

A wrong parameter only shows up inside the job, when it runs:

```csharp
public class ReportJob : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        var hardCast = (ReportRequest)context.Parameter!;       // InvalidCastException: the run is Faulted
        var softCast = context.Parameter as ReportRequest;      // null: the job silently does nothing useful
        return Task.CompletedTask;
    }
}

jobRegistry.RunInstantJob<ReportJob>("weekly"); // compiles, but ReportJob expects a ReportRequest
```

To keep such mistakes visible:

- Use a dedicated parameter type, for example a `record`, instead of primitives like `string` or `int`.
- Check the parameter at the start of the job and fail with a clear message:

  ```csharp
  if (context.Parameter is not ReportRequest request)
  {
      throw new ArgumentException($"Expected {nameof(ReportRequest)} but got '{context.Parameter?.GetType().Name ?? "null"}'.");
  }
  ```

- When jobs are triggered from the [dashboard](dashboard.md), configure a default parameter with `WithParameter`. The dashboard deserializes the JSON into the type of that parameter. Without one it can't know the expected type and passes the text instead.

## Parameters are not immutable

Passed in parameters are not immutable by default or cloned throughout the job execution. This means that if you change the parameter in the job, it will also change in the next execution. If you need to keep the parameter unchanged, you should clone it in the job.

```csharp
public class MyParameter
{
  public int Counter { get; set; }
}

Services.AddNCronJob(b =>
{
  b.AddJob<MyJob>(p => p.WithCronExpression(...));
});

public class MyJob : IJob
{
  public Task RunAsync(IJobExecutionContext context, CancellationToken token)
  {
     var myParam = (MyParameter)context.Parameter;
     myParam.Counter++; // This will be incremented with each job run
  }
}
```

If `MyJob` runs twice already and is invoked a third time, `myParam.Counter` will be 2 when the function gets invoked.
