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

Subscribers to the reporting service will receive an immutable instance of the `ExecutionProgress`. This type will expose every meaningful change to any job or orchestration handled by NCronJob.

### Sample usage

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
        IDisposable subscriber = reporter.Register(Subscriber);

        await app.RunAsync();

        // ...and when you're done with it, unhook the subscription.
        subscriber.Dispose();

        void Subscriber(ExecutionProgress progress)
        {
            if (progress.RunId is null)
            {
                logger.LogWarning(
                    "Orchestration {CorrelationId} - {Status}",
                    progress.CorrelationId,
                    progress.State);

                return;
            }

            logger.LogWarning("Job {JobRunId} - {Status}",
                progress.RunId,
                progress.State);
        }
    }

}
```

Given the orchestration defined above, with jobs of varying durations, the generated output log may look like this:

```text
10:46:47 warn: Program[0] Orchestration d36e2b62-6997-44c5-a9f9-de442b8a1807 - OrchestrationStarted
10:46:50 warn: Program[0] Job d751f2eb-9f8d-46e3-b863-6dadc6498468 - NotStarted
10:46:50 warn: Program[0] Job d751f2eb-9f8d-46e3-b863-6dadc6498468 - Scheduled
10:47:00 warn: Program[0] Job d751f2eb-9f8d-46e3-b863-6dadc6498468 - Initializing
10:47:00 warn: Program[0] Job d751f2eb-9f8d-46e3-b863-6dadc6498468 - Running
10:47:00 info: A[0] [A]: Starting processing...
10:47:01 info: A[0] [A]: Processing is done.
10:47:01 warn: Program[0] Job d751f2eb-9f8d-46e3-b863-6dadc6498468 - Completing
10:47:01 warn: Program[0] Job d751f2eb-9f8d-46e3-b863-6dadc6498468 - WaitingForDependency
10:47:01 warn: Program[0] Job 27509f28-d84b-4d50-8f9d-e5500bbc17fa - NotStarted
10:47:01 warn: Program[0] Job f28acd5e-cd3e-445e-979c-59a160035ef2 - NotStarted
10:47:01 warn: Program[0] Job d751f2eb-9f8d-46e3-b863-6dadc6498468 - Completed
10:47:01 warn: Program[0] Job 27509f28-d84b-4d50-8f9d-e5500bbc17fa - Initializing
10:47:01 warn: Program[0] Job 27509f28-d84b-4d50-8f9d-e5500bbc17fa - Running
10:47:01 info: B[0] [B]: Starting processing...
10:47:01 warn: Program[0] Job f28acd5e-cd3e-445e-979c-59a160035ef2 - Initializing
10:47:01 warn: Program[0] Job f28acd5e-cd3e-445e-979c-59a160035ef2 - Running
10:47:01 info: C[0] [C]: Starting processing...
10:47:02 info: C[0] [C]: Processing is done.
10:47:02 warn: Program[0] Job f28acd5e-cd3e-445e-979c-59a160035ef2 - Completing
10:47:02 warn: Program[0] Job f28acd5e-cd3e-445e-979c-59a160035ef2 - WaitingForDependency
10:47:02 warn: Program[0] Job f4a8ef3b-4848-4363-a3b7-f847562598b3 - NotStarted
10:47:02 warn: Program[0] Job f28acd5e-cd3e-445e-979c-59a160035ef2 - Completed
10:47:03 warn: Program[0] Job f4a8ef3b-4848-4363-a3b7-f847562598b3 - Initializing
10:47:03 warn: Program[0] Job f4a8ef3b-4848-4363-a3b7-f847562598b3 - Running
10:47:03 info: D[0] [D]: Starting processing...
10:47:04 info: D[0] [D]: Processing is done.
10:47:04 warn: Program[0] Job f4a8ef3b-4848-4363-a3b7-f847562598b3 - Completing
10:47:04 warn: Program[0] Job f4a8ef3b-4848-4363-a3b7-f847562598b3 - Completed
10:47:07 info: B[0] [B]: Processing is done.
10:47:07 warn: Program[0] Job 27509f28-d84b-4d50-8f9d-e5500bbc17fa - Completing
10:47:07 warn: Program[0] Job 27509f28-d84b-4d50-8f9d-e5500bbc17fa - Completed
10:47:07 warn: Program[0] Orchestration d36e2b62-6997-44c5-a9f9-de442b8a1807 - OrchestrationCompleted
```

## Known limitations

As global NCronJob observability is still under development, it's not feature complete yet.

Below are know missing parts of it. Would you find other releated areas of interest that may be worth investigating, please submit a request in the issue tracker.

- ~~[Report removed jobs as `Cancelled`](https://github.com/NCronJob-Dev/NCronJob/issues/161)~~
- ~~[Report skipped child jobs as `Skipped`](https://github.com/NCronJob-Dev/NCronJob/issues/160)~~
