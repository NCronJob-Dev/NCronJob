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
    public Task RunAsync(JobExecutionContext context, CancellationToken token)
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
