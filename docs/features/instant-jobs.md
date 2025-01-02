# Triggering instant jobs

**NCronJob** allows you to trigger jobs instantly. This is useful when you want to run a job immediately without waiting for the next scheduled time. For example you get a API request and want to offload the work to a background job immediately.

Instant jobs are like "regular" CRON jobs and inherit from `IJob`. The only difference is that they are triggered manually.
So also CRON jobs can be triggered instantly.

```csharp
public class MyJob : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        ParameterDto dto = (ParameterDto)context.Parameter;
        // Do something
        return Task.CompletedTask;
    }
}
```

As CRON jobs, they have to be registered in the `AddNCronJob` method.

```csharp
Services.AddNCronJob(options => 
{
    options.AddJob<MyJob>(); // No need to specify a CRON expression
});
```

There is no need of a CRON expression for instant jobs. Also passing in parameters doesn't do anything, they will be passed in differently. Let's have a look at how to trigger an instant job. Imagine we have a Minimal API where we want to send an E-Mail:

```csharp
app.MapPost("/send-email", (RequestDto dto, IInstantJobRegistry jobRegistry) => 
{
    var parameterDto = new ParameterDto
    {
        Email = dto.Email,
        Subject = dto.Subject,
        Body = dto.Body
    };

    jobRegistry.RunInstantJob<MyJob>(parameterDto);
    return Results.Ok();
});
```

The `RunInstantJob` method takes the job type and the parameters as arguments. Optionally you can pass in a `CancellationToken` as well. The job will be executed immediately.

## Starting a job with a delay
If you find the need of delaying the execution of an instant job, you can use the `RunScheduledJob` method with a `TimeSpan` as a delay. The same as `RunInstantJob` applies here, the job has to be registered in the `AddNCronJob` method.

```csharp
app.MapPost("/send-email", (RequestDto dto, IInstantJobRegistry jobRegistry) => 
{
    var parameterDto = new ParameterDto
    {
        Email = dto.Email,
        Subject = dto.Subject,
        Body = dto.Body
    };

    jobRegistry.RunScheduledJob<MyJob>(TimeSpan.FromMinutes(5), parameterDto);
    return Results.Ok();
});
```

## Starting a job at a specific date and time
If you want to start a job at a specific date and time, you can use the `RunScheduledJob` method with a `DateTimeOffset` as a parameter. The same as before: The job has to be registered.

```csharp
app.MapPost("/send-email", (RequestDto dto, IInstantJobRegistry jobRegistry) => 
{
    var parameterDto = new ParameterDto
    {
        Email = dto.Email,
        Subject = dto.Subject,
        Body = dto.Body
    };

    jobRegistry.RunScheduledJob<MyJob>(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)), parameterDto);
    return Results.Ok();
});
```

## Priority
Instant jobs are executed with a higher priority than CRON jobs. This means that if you have a CRON job that is scheduled to run at the same time as an instant job, the instant job will be executed first (and if both of them are competing for the same resources, the instant job will be executed).

## Minimal API
Running instant jobs can also be done with the minimal API ([Minimal API](minimal-api.md)), which allows to create an anonymous lambda, that can also contain dependencies.

```csharp
app.MapPost("/send-email", (RequestDto dto, IInstantJobRegistry jobRegistry) => 
{
    var parameterDto = new ParameterDto
    {
        Email = dto.Email,
        Subject = dto.Subject,
        Body = dto.Body
    };

    jobRegistry.RunInstantJob(async (HttpClient httpClient) => 
    {
        await httpClient.PostAsync("https://api.example.com/send-email", new StringContent(JsonSerializer.Serialize(parameterDto)));
    });
    return TypedResults.Ok();
});
```

## Instrumentation

All members of the `IInstantJobRegistry` interface return the correlation id of the triggered job (See [*"Tracing requests of dependencies via `CorrelationId`"*](./model-dependencies.md#tracing-requests-of-dependencies-via-correlationid).).

```csharp
Guid oneCorrelationId = jobRegistry.RunInstantJob<MyJob>();

Guid anotherCorrelationId = jobRegistry.RunScheduledJob<MyJob>(TimeSpan.FromMinutes(5));

[...]
```
