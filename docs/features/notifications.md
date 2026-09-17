# Notifications

**NCronJob** provides a way to get notified when a job is done. For this, implement a `IJobNotificationHandler<TJob>`
and register it in your DI container.

```csharp
builder.Services.AddNCronJob(options =>
    options.AddJob<PrintHelloWorld>(j => 
    {
        // Every minute and optional parameter
        j.WithCronExpression("* * * * *")
         .WithParameter("Hello World");
    })
    .AddNotificationHandler<MyJobNotificationHandler>());
```

This allows to run logic after a job is done. The `JobExecutionContext` and the `Exception` (if there was one) are
passed to the `HandleAsync` method.

!!! info "Service scope"
    The notification handler is resolved from its **own** dependency injection scope. Scoped services (for example an
    Entity Framework `DbContext`) are therefore **not** shared with the job instance. Pass data from the job to the
    handler via `IJobExecutionContext.Output` instead.

Exceptions thrown by a notification handler are caught and don't affect the job outcome.

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
