# Notifications

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
    .AddNotificationHandler<MyJobNotificationHandler>());
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

    public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken token)
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
