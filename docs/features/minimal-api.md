# Minimal API

The minimal job API offers another way of defining a cron job. It favors simplicity over feature richness. A job can be defined as such:

```csharp
builder.Services.AddNCronJob(() => { }, "0 * * * *");
```

This call will register all the necessary services and the job in question. The method can be called multiple times to register multiple jobs:

```csharp
builder.Services.AddNCronJob(() => { }, "0 * * * *");
builder.Services.AddNCronJob(() => { }, "1 * * * *");
```

The minimal job API does support resolving services from the DI container. The following example demonstrates how to resolve a logger and a `TimeProvider`:

```csharp
builder.Services.AddNCronJob((ILogger<Program> logger, TimeProvider timeProvider) =>
{
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
}, "*/5 * * * * *");
```

Also the `JobExecutionContext` and `CancellationToken` can be resolved from the DI container:

```csharp
builder.Services.AddNCronJob((JobExecutionContext context, CancellationToken token) =>
{
    
}, "*/5 * * * * *");
```

The `token` can be used to get notified when the job should be cancelled (for example if the whole application gets shut down).

Of course functions can be asynchrnous in nature as well:
```csharp
builder.Services.AddNCronJob(async (HttpClient httpClient) =>
{
    using var response = await httpClient.GetAsync("https://github.com/NCronJob-Dev/NCronJob");
    var content = await response.Content.ReadAsStringAsync();
}, "*/5 * * * * *");
```

There are certain restrictions that can't be enforced at compile time (due to a missing interface) which will lead to a runtime exception:
 
 * The job has to return `void` or `Task`, otherwise an `InvalidOperationException` will be thrown.
  
## Retry-Support
The minimal job API does support the [**Retry** model](retry-support.md) as well. The minimal job API leverages the fact that lambdas also can have attributes:

```csharp
builder.Services.AddNCronJob([RetryPolicy(retryCount: 3)] () => { }, "0 * * * *");
```

To know in which attempt the job currently is, the `JobExecutionContext` can be used:

```csharp
builder.Services.AddNCronJob([RetryPolicy(retryCount: 3)] (JobExecutionContext context) => 
{
    if(context.Attempts == 1)
    {
        // First attempt
    }
}, "0 * * * *");
```

## Time Zone
The time zone can be controlled as well and defaults to UTC if not specified:

```csharp
builder.Services.AddNCronJob(() => { }, "0 * * * *", TimeZoneInfo.Local);
```

## Concurrency-Support
In the same way, the concurrency level can be controlled (see [**Concurrency**](concurrency-control.md)):

```csharp
builder.Services.AddNCronJob([SupportsConcurrency(2)] () => { }, "0 * * * *");
```

Now, the job can only be executed by two instances at the same time.

## Restrictions

The minimal API has some restrictions over the "full approach":

 * Some errors can only be detected at runtime (for example if the job does not return `void` or `Task`).
 * No support for `IJobNotificationHandler`
 * No support of defining dependencies between anonymous jobs

## Minimal API for instant Jobs
The minimal API also supports instant jobs, for this check out the [Instant Jobs](instant-jobs.md) documentation.
