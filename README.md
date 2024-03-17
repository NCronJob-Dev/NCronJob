<h1 align="center">NCronJob</h1>

<p align="center">
  <img src="assets/logo_small.png" alt="logo" width="120px" height="120px"/>
  <br>
  <em>Scheduling made easy</em>
  <br>
</p>

[![.NET](https://github.com/linkdotnet/NCronJob/actions/workflows/dotnet.yml/badge.svg)](https://github.com/linkdotnet/NCronJob/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/dt/LinkDotNet.NCronJob.svg)](https://www.nuget.org/packages/LinkDotNet.NCronJob)
[![NuGet](https://img.shields.io/nuget/vpre/LinkDotNet.NCronJob.svg)](https://www.nuget.org/packages/LinkDotNet.NCronJob)

# NCronJob
A Job Scheduler sitting on top of `IHostedService` in dotnet.

Often times one finds themself between the simplicisty of the `BackgroundService`/`IHostedService` and the complexity of a full blown `Hangfire` or `Quartz` scheduler. 
This library aims to fill that gap by providing a simple and easy to use job scheduler that can be used in any dotnet application and feels "native".

## Features
- [x] The ability to schedule jobs using a cron expression
- [x] The ability to instantly run a job
- [x] Parameterized jobs - instant as well as cron jobs!
- [x] Integrated in ASP.NET - Access your DI container like you would in any other service
- [ ] Get notified when a job is done (either successfully or with an error) - currently in development

## Not features

As this is a simple scheduler, some features are not included by design. If you need these features, you might want to look into a more advanced scheduler like `Hangfire` or `Quartz`.

- [ ] Job persistence - Jobs are not persisted between restarts of the application.
- [ ] Job history - There is no history of jobs that have been run.
- [ ] Retries - If a job fails, it is not retried.
- [ ] Progress state - There is no way to track the progress of a job. The library will support notifying when a job is done, but not the progress of the job itself.

## Short example
1. Import the namespace (or let your IDE do the dirty work)
```csharp
using LinkDotNet.NCronJob;
```

2. Create a job
```csharp
public class PrintHelloWorld : IJob
{
    private readonly ILogger<PrintHelloWorld> logger;

    public PrintHelloWorld(ILogger<PrintHelloWorld> logger)
    {
        this.logger = logger;
    }

	public Task Run(JobExecutionContext context, CancellationToken token = default)
    {
    	logger.LogInformation("Hello World");
    	logger.LogInformation("Parameter: {Parameter}", context.Parameter);

        return Task.CompletedTask;
    }
}
```

3. Register the job in your `Program.cs`
```csharp
builder.Services.AddCronJob<PrintHelloWorld>(options => 
{
	// Every minute
	options.CronExpression = "* * * * * *";
	// Optional parameter
	options.Parameter = "Hello World";
});
```

4. Run your application and see the magic happen

## Triggering an instant job
If the need arises and you want to trigger a job instantly, you can do so:
```csharp
public class MyService
{
  private readonly IInstantJobRegistry jobRegistry;
  
  public MyService(IInstantJobRegistry jobRegistry) => this.jobRegistry = jobRegistry;

  public void MyMethod() => jobRegistry.AddInstantJob<MyJob>("I am an optional parameter");
}
```

## Retrieving scoped services
Every job is registered as singleton inside the container, so be careful if you have state and furthermore be careful when retrieving scoped services.
To come around this "limitation", you can simply create your own scope inside the job:

```csharp
public class JobWithScope : IJob
{
	private readonly IServiceProvider services;

	public JobWithScope(IServiceProvider services) => this.services = services;
	public async Task Run(JobExecutionContext context, CancellationToken token = default)
	{
		using var scope = services.CreateScope();
		var myScopedService = scope.ServiceProvider.GetRequiredService<MyScopedService>();
		await myScopedService.DoSomething();
	}
}
```
