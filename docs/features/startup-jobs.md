# Running Startup Jobs

**NCronJob** allows you to configure jobs to run at application startup. This is useful for tasks that need to be executed immediately when the application starts, such as initial data loading, cleanup tasks, or other setup procedures.

Startup jobs are defined like regular CRON jobs and inherit from `IJob`. The only difference is that they are configured to run at startup.  **All** startup jobs must be completed prior to the start of any other CRON (or instant) jobs.

```csharp
public class MyStartupJob : IJob
{
    public Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        // Perform startup task
        return Task.CompletedTask;
    }
}

```

As with CRON jobs, they must be registered in the `AddNCronJob` method.

```csharp
Services.AddNCronJob(options => 
{
    options.AddJob<MyStartupJob>()
           .RunAtStartup(); // Configure the job to run at startup
});
```

The `RunAtStartup` method ensures that the job is executed as soon as the application starts. This method is useful for scenarios where certain tasks need to be performed immediately upon application launch.

## Example Use Case

Consider an application that needs to load initial data from a database or perform some cleanup tasks whenever it starts. You can define and configure a startup job to handle this:

### Job Definition
```csharp
public class InitialDataLoader : IJob
{
    private readonly IDataService _dataService;

    public InitialDataLoader(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        await _dataService.LoadInitialDataAsync();
    }
}
```

### Registering the Job

In your `Program.cs` or `Startup.cs` file, register the job and configure it to run at startup:

```csharp
builder.Services.AddNCronJob(options => 
{
    options.AddJob<InitialDataLoader>()
           .RunAtStartup();
});
```

This setup ensures that the `InitialDataLoader` job will be executed as soon as the application starts, loading the necessary initial data.


## Summary

Startup jobs are a powerful feature of **NCronJob** that enable you to execute critical tasks immediately upon application startup. By using the `RunAtStartup` method, you can ensure that your application performs necessary setup procedures, data loading, or cleanup tasks right at the beginning of its lifecycle.

This feature is particularly useful for applications that require certain operations to be completed before they are fully functional. By configuring startup jobs, you can streamline your application's initialization process and improve its overall reliability and performance.
