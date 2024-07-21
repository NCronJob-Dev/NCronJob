# Passing parameters to a job

Often times a job needs some kind of configuration or parameter to run. Imagine you have a job that generates a report and can run daily, weekly or monthly. You could create three different jobs for each frequency, but that would be a lot of duplicated code. Instead, you can pass in parameters to the job.

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

## Parameters are not immutable
Passed in parameters are not immutable by default or cloned through out the job execution. This means that if you change the parameter in the job, it will also change in the next execution. If you need to keep the parameter unchanged, you should clone it in the job.

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
