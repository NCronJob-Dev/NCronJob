<h1 align="center">NCronJob</h1>

<p align="center">
  <img src="assets/logo_small.png" alt="logo" width="120px" height="120px"/>
  <br>
  <em>Scheduling made easy</em>
  <br>
</p>

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
builder.Services.AddNCronJob<PrintHelloWorld>(options => 
{
	// Every minute
	options.CronExpression = "* * * * * *";
	// Optional parameter
	options.Parameter = "Hello World";
});
```

4. Run your application and see the magic happen
