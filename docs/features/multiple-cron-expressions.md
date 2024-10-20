# Registering multiple CRON expressions
The fluent builder allows you to register multiple CRON expressions for a single job. This is useful when you want to run a job at different times of the day. For example:

```csharp
Services.AddNCronJob(options =>
{
    // Register a job that runs at midnight and midday
    options.AddJob<ExampleJob>(j =>
    {
        j.WithCronExpression("0 0 * * *") // Run at midnight
         .And
         .WithCronExpression("0 12 * * *"); // Run at midday
    });
});
```

The `And` method is used to chain multiple CRON expressions together. You can chain as many expressions as you like.
If a given job has the same CRON expression registered multiple times, it will be executed multiple times as well.

```csharp
Services.AddNCronJob(options =>
{
    // Register a job that runs every 5 minutes
    options.AddJob<ExampleJob>(j =>
    {
        j.WithCronExpression("*/5 * * * *") // Run every 5 minutes
         .And
         .WithCronExpression("*/5 * * * *"); // Run every 5 minutes
    });
});
```

Two instances of the `ExampleJob` class will be created and executed every 5 minutes.
