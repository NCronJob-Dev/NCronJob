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
Registering the same CRON expression twice for a job throws an `InvalidOperationException` at startup. Give each registration a distinct name with `WithName` or a different parameter if the duplication is intended.
