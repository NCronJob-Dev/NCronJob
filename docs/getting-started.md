# Getting Started

Using **NCronJob** is simple and easy. Just follow the steps below to get started.

## 1. Install the package
[![NuGet](https://img.shields.io/nuget/vpre/NCronJob.svg)](https://www.nuget.org/packages/NCronJob)

Install the latest stable version of the package via NuGet:

```bash
dotnet add package NCronJob
```

Alternatively add the package reference to your `.csproj` file:

```xml
<PackageReference Include="NCronJob" Version="#{version}#" />
```

## 2. Create a job
**NCronJob** offers a single way of defining jobs: by implementing the `IJob` interface with a single `RunAsync` method:

```csharp
public class PrintHelloWorld : IJob
{
    private readonly ILogger<PrintHelloWorld> logger;

    public PrintHelloWorld(ILogger<PrintHelloWorld> logger)
    {
        this.logger = logger;
    }

    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation("Hello World");
        logger.LogInformation("Parameter: {Parameter}", context.Parameter);

        return Task.CompletedTask;
    }
}
```

## 3. Register the service and the job
The **NCronJob** library provides one easy entry point for all its magic, the `AddNCronJob` extension method on top of the `IServiceCollection` interface. 

```csharp
Services.AddNCronJob(options => 
{
    options.AddJob<PrintHelloWorld>(j => 
    {
        // Every minute and optional parameter
        j.WithCronExpression("* * * * *")
         .WithParameter("Hello World");
    }));
});
```

Now your `PrintHelloWorld` job will run every minute and log "Hello World" to the console. And that is all!

## Too complicated?
We also over a "Minimal API" that allows you to define jobs similiar to the Minimal API for Controllers.

```csharp
builder.Services.AddNCronJob((ILogger<Program> logger, TimeProvider timeProvider) =>
{
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
}, "*/5 * * * * *");
```

The job will be defined "inline" and is capable of resolving services from the DI container.

You can read more about this in the section [Minimal API](features/minimal-api.md).
