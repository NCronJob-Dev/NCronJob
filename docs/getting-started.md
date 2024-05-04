# Getting Started

Using **NCronJob** is simple and easy. Just follow the steps below to get started.

## 1. Install the package
[![NuGet](https://img.shields.io/nuget/vpre/LinkDotNet.NCronJob.svg)](https://www.nuget.org/packages/LinkDotNet.NCronJob)

Install the latest stable version of the package via NuGet:

```bash
dotnet add package LinkDotNet.NCronJob
```

Alternatively add the package reference to your `.csproj` file:

```xml
<PackageReference Include="LinkDotNet.NCronJob" Version="2.1.4" />
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

    public Task RunAsync(JobExecutionContext context, CancellationToken token)
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
