# Observing job progress

Every time one schedules a job (or triggers it as an instant job), a virtual orchestration is spawned.

An orchestration can be as simple as a unique job, or as complex as a root job and the whole hierarchy of its direct and indirect dependent jobs (see [*"Model Dependencies"*](../features/model-dependencies.md)).

An orchestration is uniquely identifed by an identifier. All jobs belonging to an orchestration share this same `CorrelationId` (See [*"Tracing requests of dependencies via `CorrelationId`"*](../features/model-dependencies.md#tracing-requests-of-dependencies-via-correlationid)).

From a timeline perspective, an orchestration starts before the root job that initiated it and completes when **all** of its leaf jobs have reached a final state.

## Subscribing to the executions of jobs

### Forewords

!!! warning

    This is an **experimental** feature subject to breaking changes independently of the standard semver lifecycle release of **NCronJob**.

    While reporting feedback or bugs about it, please do not forget to
    mention in the issue which version of NCronJob you're using.

Would you decide to give it an early try, in order to suppress the warnings that comes with the [.NET Experimental attribute](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/preview-apis#experimentalattribute), update your `.csproj` with a `<NoWarn>` project setting:

```xml
<PropertyGroup>
    ...
    <NoWarn>$(NoWarn);NCRONJOB_OBSERVER</NoWarn>
</PropertyGroup>
```

Alternatively, it can also be silenced through an `.editorconfig` setting.

```text
[*.cs]

...

# NCRONJOB_OBSERVER: Type is for evaluation purposes only and is subject to change or removal in future updates.
dotnet_diagnostic.NCRONJOB_OBSERVER.severity = none
```

### Registering a notifier callback

**NCronJob** exposes the capability to notify whenever jobs change states. One can
suscribe to this by leveraging the `IJobExecutionProgressReporter` service.

This is done through the following exposed method

```csharp
IDisposable Register(Action<ExecutionProgress> callback);
```

!!! info

    The registration returns the subscription as a `IDisposable` object.
    In order to stop the callback from receiving notifications anymore, invoke the `Dispose()` method of it.

Subscribers to the reporting service will receive an immutable instance of the `ExecutionProgress`. This type will expose every meaningful change to any job or orchestration handled by **NCronJob**.

### Described ExecutionProgress

An `ExecutionProgress` instance exposes the following properties:

- **Timestamp**: The instant this `ExecutionProgress` instance was created.
  
  Will be `null` when the `ExecutionProgress` instance relates to the start or completion of an orchestration.

- **CorrelationId**: The correlation identifier of an orchestration run. 
  
  Will decorate every reported progress of the root job and all of its dependencies.

- **RunId**: The identifier of a job run within an orchestration. 
  
  Will be `null` when the `ExecutionProgress` instance relates to the start or completion of an orchestration.

- **ParentRunId**: The identifier of the parent job run.
  
  Will be `null` when the reported instance is the root job of an orchestration, or when the `ExecutionProgress` instance relates to the start or completion of an orchestration.

- **State**: The reported state.
  
  Will either relate to an orchestration, describing its start or completion or to a job belonging to an orchestration.

- **Name**: The optional custom name given to the job.
  
  Will be `null` when no name was specified or when the `ExecutionProgress` instance relates to the start or completion of an orchestration.

- **Type**: The type of the job.
  
  Will be `null` if the job is an anonymous function based job or when the `ExecutionProgress` instance relates to the start or completion of an orchestration.

- **IsTypedJob**: Whether the job is a class based job (implementing `IJob`) or not. 
  
  Will be `null` when the reported instance is the root job of an orchestration, or when the `ExecutionProgress` instance relates to the start or completion of an orchestration.

## Sample usage

Considering the following orchestration

```text
A ─┬─ (successful) ──> B
   └─ (successful) ──> C ─── (successful) ──> D
```

Below a very simple approach to schedule it every minute and register a subscriber.

```csharp
using NCronJob;

public class A : IJob
{
    public A(ILogger<A> logger) => Logger = logger;

    public ILogger<A> Logger { get; }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Logger.LogInformation("[A]: Starting processing...");

        await Task.Delay(TimeSpan.FromSeconds(1), token);

        Logger.LogInformation("[A]: Processing is done.");
    }
}

public class B : IJob
{
    public B(ILogger<B> logger) => Logger = logger;

    public ILogger<B> Logger { get; }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Logger.LogInformation("[B]: Starting processing...");

        await Task.Delay(TimeSpan.FromSeconds(6), token);

        Logger.LogInformation("[B]: Processing is done.");
    }
}

public class C : IJob
{
    public C(ILogger<C> logger) => Logger = logger;

    public ILogger<C> Logger { get; }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Logger.LogInformation("[C]: Starting processing...");

        await Task.Delay(TimeSpan.FromSeconds(1), token);

        Logger.LogInformation("[C]: Processing is done.");
    }
}

public class D : IJob
{
    public D(ILogger<D> logger) => Logger = logger;

    public ILogger<D> Logger { get; }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Logger.LogInformation("[D]: Starting processing...");

        await Task.Delay(TimeSpan.FromSeconds(1), token);

        Logger.LogInformation("[D]: Processing is done.");
    }
}

public class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddNCronJob(n =>
        {
            n.AddJob<D>();

            n.AddJob<C>()
                .ExecuteWhen(success: s => s.RunJob<D>());

            n.AddJob<B>();

            n.AddJob<A>(o => o.WithCronExpression("* * * * *"))
                .ExecuteWhen(success: s => s.RunJob<B>())
                .ExecuteWhen(success: s => s.RunJob<C>());
        });

        var app = builder.Build();

        await app.UseNCronJobAsync();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        // Retrieve the observer service from the DI container...
        var reporter = app.Services.GetRequiredService<IJobExecutionProgressReporter>();

        // ...enlist a new subscriber to it...
        IDisposable subscription = reporter.Register(Subscriber);

        await app.RunAsync();

        // ...and when you're done with it, unhook the subscription.
        subscription.Dispose();

        void Subscriber(ExecutionProgress progress)
        {
            if (progress.RunId is null)
            {
                logger.LogWarning(
                    "Orchestration {CorrelationId} - {Status}",
                    progress.CorrelationId.ToString()[1..8],
                    progress.State);

                return;
            }

            logger.LogWarning("Job {CorrelationId}/{JobRunId} - {Type} - {Status}",
                progress.CorrelationId.ToString()[1..8],
                progress.RunId.ToString()[1..8],
                progress.Type,
                progress.State);
        }
    }
}
```

Given the orchestration defined above, with jobs of varying durations, the generated output log may look like this:

```text
23:15:25 warn: Orchestration 1798344 - OrchestrationStarted
23:15:25 warn: Job 1798344/3ffcf52 - A - NotStarted
23:15:25 warn: Job 1798344/3ffcf52 - A - Scheduled
23:16:00 warn: Orchestration c92cbfa - OrchestrationStarted
23:16:00 warn: Job c92cbfa/b93b29e - A - NotStarted
23:16:00 warn: Job c92cbfa/b93b29e - A - Scheduled
23:16:00 warn: Job 1798344/3ffcf52 - A - Initializing
23:16:00 warn: Job 1798344/3ffcf52 - A - Running
23:16:00 info: [A]: Starting processing...
23:16:01 info: [A]: Processing is done.
23:16:01 warn: Job 1798344/3ffcf52 - A - Completing
23:16:01 warn: Job 1798344/3ffcf52 - A - WaitingForDependency
23:16:01 warn: Job 1798344/6624e8d - B - NotStarted
23:16:01 warn: Job 1798344/cc4d75c - C - NotStarted
23:16:01 warn: Job 1798344/3ffcf52 - A - Completed
23:16:01 warn: Job 1798344/6624e8d - B - Initializing
23:16:01 warn: Job 1798344/6624e8d - B - Running
23:16:01 warn: Job 1798344/cc4d75c - C - Initializing
23:16:01 warn: Job 1798344/cc4d75c - C - Running
23:16:01 info: [B]: Starting processing...
23:16:01 info: [C]: Starting processing...
23:16:02 info: [C]: Processing is done.
23:16:02 warn: Job 1798344/cc4d75c - C - Completing
23:16:02 warn: Job 1798344/cc4d75c - C - WaitingForDependency
23:16:02 warn: Job 1798344/938f4ea - D - NotStarted
23:16:02 warn: Job 1798344/cc4d75c - C - Completed
23:16:03 warn: Job 1798344/938f4ea - D - Initializing
23:16:03 warn: Job 1798344/938f4ea - D - Running
23:16:03 info: [D]: Starting processing...
23:16:04 info: [D]: Processing is done.
23:16:04 warn: Job 1798344/938f4ea - D - Completing
23:16:04 warn: Job 1798344/938f4ea - D - Completed
23:16:07 info: [B]: Processing is done.
23:16:07 warn: Job 1798344/6624e8d - B - Completing
23:16:07 warn: Job 1798344/6624e8d - B - Completed
23:16:07 warn: Orchestration 1798344 - OrchestrationCompleted
23:17:00 warn: Job c92cbfa/b93b29e - A - Initializing
23:17:00 warn: Orchestration d29d000 - OrchestrationStarted
23:17:00 warn: Job d29d000/9e6a217 - A - NotStarted
23:17:00 warn: Job c92cbfa/b93b29e - A - Running
23:17:00 info: [A]: Starting processing...
23:17:00 warn: Job d29d000/9e6a217 - A - Scheduled
23:17:01 info: [A]: Processing is done.

[...]
```

## Known limitations

As global NCronJob observability is still under development, it's not feature complete yet.

Below are know missing parts of it. Would you find other releated areas of interest that may be worth investigating, please submit a request in the issue tracker.

- ~~[Report removed jobs as `Cancelled`](https://github.com/NCronJob-Dev/NCronJob/issues/161)~~
- ~~[Report skipped child jobs as `Skipped`](https://github.com/NCronJob-Dev/NCronJob/issues/160)~~
