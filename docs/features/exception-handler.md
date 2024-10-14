# Exception Handler

**NCronJob**, much like **ASP.NET**, offers a central way to handle exceptions. This is useful when you want to log exceptions or send an E-Mail when a job fails. The exception handler has to inherit from the `IExceptionHandler` interface.

```csharp
public class MyExceptionHandler : IExceptionHandler
{
    public Task<bool> TryHandleAsync(JobExecutionContext context, Exception exc, CancellationToken token)
    {
        // Handle the exception
        return Task.FromResult(true);
    }
}
```

If the `TryHandleAsync` method returns `true`, the exception is considered as handled and no other exception handler will be called. If it returns `false`, the next exception handler will be called, if available.

Therefore the order of registration is important. The first registered exception handler will be called first.

## Order of execution with NotificationHandlers
All exception handlers are executed before the `IJobNotificationHandler` for that specific job is called. Independent if the exception handler returns `true` or `false`, the `IJobNotificationHandler` will get the exception passed in.