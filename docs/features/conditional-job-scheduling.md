# Conditional Job Scheduling with OnlyIf

Schedule jobs that only execute when specific runtime conditions are met. This feature allows you to control job execution based on feature flags, configuration values, cached state, or any other runtime condition—without wasting resources instantiating jobs that won't run.

## The Problem

Traditionally, if you needed conditional job execution, you had to check the condition inside the job's `RunAsync` method:

```csharp
public class MyJob : IJob
{
    private readonly IFeatureFlagService _featureFlags;
    
    public MyJob(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags;
    }
    
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        // Job is instantiated even if it won't run
        if (!_featureFlags.IsEnabled("my-feature"))
            return Task.CompletedTask; // Wasted instantiation
        
        // Actual work...
    }
}
```

**Issues with this approach:**
- Job class is instantiated and dependencies resolved even when skipped
- Conditional logic is hidden inside the job
- Job appears as "executed" in logs/metrics when it did nothing
- Wastes resources on dependency injection and object creation

## The Solution: OnlyIf

The `OnlyIf` method allows you to specify conditions that are evaluated **before** job instantiation. You can use `OnlyIf` directly or chain it with other configuration methods:

```csharp
builder.Services.AddNCronJob(options =>
{
    // Direct usage without chaining
    options.AddJob<MyJob>(p => p
        .OnlyIf(() => GlobalCache.IsFeatureEnabled)
        .WithCronExpression("*/5 * * * *"));
    
    // Or chain after other methods
    options.AddJob<MyJob>(p => p
        .WithCronExpression("*/5 * * * *")
        .OnlyIf(() => GlobalCache.IsFeatureEnabled));
});
```

**Benefits:**
- Jobs are only instantiated when conditions are satisfied
- Conditional logic is explicit in job registration
- Proper "Skipped" state in logs/metrics
- Saves resources by avoiding unnecessary instantiation
- Works with both scheduled (cron) and instant jobs

## Usage Examples

### Simple Predicate

Evaluate a simple boolean condition:

```csharp
var isMaintenanceMode = false;

builder.Services.AddNCronJob(options =>
{
    options.AddJob<BackupJob>(p => p
        .OnlyIf(() => !isMaintenanceMode) // Skip during maintenance
        .WithCronExpression("0 2 * * *")); // Daily at 2 AM
});
```

### With Dependency Injection

Access services from DI to make decisions:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<DataSyncJob>(p => p
        .WithCronExpression("*/10 * * * *")
        .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("data-sync")));
});
```

### Async Conditions

Use async methods for I/O-bound condition checks:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<ReportJob>(p => p
        .WithCronExpression("0 9 * * MON") // Every Monday at 9 AM
        .OnlyIf(async (IConfigService config) => 
            await config.IsJobEnabledAsync("weekly-report")));
});
```

### Multiple Conditions (AND Logic)

Combine multiple conditions—all must be true for execution:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<AnalyticsJob>(p => p
        .WithCronExpression("0 * * * *")
        .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("analytics"))
        .OnlyIf((ICacheService cache) => cache.Get<bool>("system-ready"))
        .OnlyIf(() => DateTime.UtcNow.Hour >= 8 && DateTime.UtcNow.Hour < 18)); // Business hours only
});
```

All conditions are evaluated in order, and the job is skipped if **any** condition returns false.

### Accessing CancellationToken

Conditions can access the cancellation token for cooperative cancellation:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<MyJob>(p => p
        .WithCronExpression("*/5 * * * *")
        .OnlyIf(async (IHealthCheckService health, CancellationToken ct) => 
            await health.IsHealthyAsync(ct)));
});
```

## How It Works

### Execution Flow

**Without OnlyIf:**
```
Cron triggers → Job instantiated → Dependencies resolved → RunAsync() executes
```

**With OnlyIf:**
```
Cron triggers → OnlyIf evaluated → (if false, skip) → Job instantiated → RunAsync() executes
                                 ↓
                            Job never created
                            Resources saved
```

### Evaluation Timing

- **Evaluated**: Once per cron trigger, before job instantiation
- **Scope**: Uses a new async scope for dependency resolution
- **Retries**: Conditions are **NOT re-evaluated** during retry attempts
- **Dependent Jobs**: Conditions can be applied to both parent jobs and dependent jobs (ExecuteWhen)

### Important: Conditions and Retries

When a job has a retry policy and its condition was initially `true`:

```csharp
[RetryPolicy(retryCount: 3)]
public class MyJob : IJob { /* ... */ }

builder.Services.AddNCronJob(options =>
{
    options.AddJob<MyJob>(p => p
        .WithCronExpression("*/5 * * * *")
        .OnlyIf(() => someCondition)); // Evaluated only once
});
```

**If the condition is `true` initially:**
1. Job is instantiated and runs
2. If it fails, retry policy kicks in
3. Condition is **NOT** re-evaluated for retries
4. Retries proceed regardless of condition state

**If the condition is `false` initially:**
1. Job is never instantiated
2. Execution is skipped entirely
3. No retries occur

This design ensures that once a job starts executing, retry behavior is consistent and not affected by changing conditions.

## Using OnlyIf with Dependent Jobs

The `OnlyIf` feature works seamlessly with dependent jobs defined using `ExecuteWhen`. This allows you to control whether dependent jobs should run based on runtime conditions:

### Basic Example

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<BlogPostPublisher>(p => p.WithCronExpression("* * * * *"))
        .ExecuteWhen(success: s => s
            .RunJob<SimilarBlogPostJob>()
                .OnlyIf(() => someCondition));
});
```

### Multiple Dependent Jobs with Different Conditions

You can apply different conditions to different dependent jobs:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<DataProcessingJob>(p => p.WithCronExpression("0 * * * *"))
        .ExecuteWhen(success: s => s
            .RunJob<EmailNotificationJob>()
                .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("email-notifications"))
            .RunJob<SlackNotificationJob>()
                .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("slack-notifications"))
            .RunJob<CleanupJob>()
                .OnlyIf(() => DateTime.UtcNow.Hour < 6)); // Only during off-hours
});
```

### With Async Conditions

Dependent jobs support async conditions just like parent jobs:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<MainJob>(p => p.WithCronExpression("0 * * * *"))
        .ExecuteWhen(success: s => s
            .RunJob<ExpensiveValidationJob>()
                .OnlyIf(async (IValidationService validator) => 
                    await validator.ShouldValidateAsync()));
});
```

### Combining Parent and Dependent Job Conditions

Both parent and dependent jobs can have conditions:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<MainJob>(p => p
            .WithCronExpression("0 * * * *")
            .OnlyIf(() => parentCondition)) // Parent job condition
        .ExecuteWhen(success: s => s
            .RunJob<FollowUpJob>()
                .OnlyIf(() => childCondition)); // Dependent job condition
});
```

In this case:
1. The parent job runs only if `parentCondition` is true
2. If the parent job runs successfully, the dependent job runs only if `childCondition` is true
3. Both conditions are evaluated independently

### Important Notes for Dependent Jobs

- **Independent Evaluation**: Each dependent job's condition is evaluated independently
- **No Instantiation on Skip**: When a condition fails, the dependent job is never instantiated
- **Chaining Support**: You can chain multiple `RunJob()` calls, each with its own `OnlyIf()` condition
- **All OnlyIf Features**: Dependent jobs support all `OnlyIf` features: simple predicates, DI, async, and multiple conditions

## Monitoring Skipped Jobs

### Condition Handlers

Register handlers to be notified when jobs are skipped due to unmet conditions:

```csharp
public class MyJobConditionHandler : IJobConditionHandler<MyJob>
{
    private readonly ILogger<MyJobConditionHandler> _logger;
    
    public MyJobConditionHandler(ILogger<MyJobConditionHandler> logger)
    {
        _logger = logger;
    }
    
    public Task HandleConditionNotMetAsync(
        JobConditionContext context, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Job {JobName} was skipped at {Time}", 
            context.JobName, 
            DateTimeOffset.UtcNow);
        
        // Could also:
        // - Update metrics
        // - Send notifications
        // - Update dashboards
        
        return Task.CompletedTask;
    }
}

// Register the handler
builder.Services.AddNCronJob(options =>
{
    options.AddJob<MyJob>(p => p
            .WithCronExpression("*/5 * * * *")
            .OnlyIf(() => someCondition))
        .AddConditionHandler<MyJobConditionHandler>();
});
```

### JobConditionContext

The context provided to condition handlers contains:

```csharp
public sealed class JobConditionContext
{
    public Guid CorrelationId { get; }        // Unique execution ID
    public string? JobName { get; }            // Custom job name
    public Type? JobType { get; }              // Job class type
    public TriggerType TriggerType { get; }    // How job was triggered
    public object? Parameter { get; }          // Job parameter
}
```

### Logging

NCronJob automatically logs condition evaluation:

- **Debug level**: When conditions are not satisfied
- **Trace level**: When conditions are satisfied

```
[DBG] Job 'MyJob' condition was not satisfied. Skipping execution.
[TRC] Job 'OtherJob' condition was satisfied. Proceeding with execution.
```

## Best Practices

### 1. Keep Conditions Fast

Conditions are evaluated on the scheduler thread. Keep them lightweight:

```csharp
// Good - Fast check
.OnlyIf(() => _cache.Get<bool>("feature-enabled"))

// Avoid - Expensive operation
.OnlyIf(async (IDatabase db) => await db.ComplexQueryAsync())
```

### 2. Use Feature Flags for Deployment Safety

Gradually roll out new jobs:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<NewExperimentalJob>(p => p
        .WithCronExpression("*/5 * * * *")
        .OnlyIf((IFeatureFlagService flags) => 
            flags.IsEnabled("experimental-job")));
});
```

### 3. Combine with Parameters

Different conditions for different parameters:

```csharp
builder.Services.AddNCronJob(options =>
{
    options
        .AddJob<DataProcessingJob>(p => p
            .WithCronExpression("0 */6 * * *")
            .WithParameter("production")
            .OnlyIf(() => isProdReady))
        .And
        .AddJob<DataProcessingJob>(p => p
            .WithCronExpression("0 */2 * * *")
            .WithParameter("staging")
            .OnlyIf(() => isStagingReady));
});
```

### 4. Document Condition Logic

Make conditions self-documenting:

```csharp
// Clear intent
.OnlyIf((ISystemStatus status) => status.IsSystemHealthy())

// Unclear
.OnlyIf((ISystemStatus status) => status.Check())
```

## Common Use Cases

### Feature Flags

```csharp
.OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("new-feature"))
```

### Maintenance Windows

```csharp
.OnlyIf(() => !MaintenanceMode.IsActive)
```

### Business Hours Only

```csharp
.OnlyIf(() => 
{
    var hour = DateTime.UtcNow.Hour;
    return hour >= 8 && hour < 18;
})
```

### System Health Checks

```csharp
.OnlyIf(async (IHealthCheckService health) => 
    await health.IsHealthyAsync())
```

### Cache Validity

```csharp
.OnlyIf((ICacheService cache) => 
    !cache.Has("recent-sync") || cache.IsExpired("recent-sync"))
```

### Configuration-Based

```csharp
.OnlyIf((IConfiguration config) => 
    config.GetValue<bool>("Jobs:DataSync:Enabled"))
```

### Instant Jobs

The `OnlyIf` feature works seamlessly with instant jobs triggered via `IInstantJobRegistry`. When a condition is not met, the instant job is skipped just like scheduled jobs:

```csharp
builder.Services.AddNCronJob(options =>
{
    options.AddJob<DataSyncJob>(p => p
        .OnlyIf((IFeatureFlagService flags) => flags.IsEnabled("data-sync")));
});

// Later in your code (e.g., API endpoint)
app.MapPost("/trigger-sync", (IInstantJobRegistry registry) => 
{
    // Job will only execute if the condition is met
    var correlationId = registry.RunInstantJob<DataSyncJob>();
    return Results.Accepted();
});
```

**Important Notes for Instant Jobs:**
- Conditions are evaluated when the instant job is triggered
- If the condition returns `false`, the job is skipped and marked as "Skipped" in the execution state
- The job is never instantiated when conditions fail, saving resources
- Works with all instant job methods: `RunInstantJob`, `RunScheduledJob`, `ForceRunInstantJob`, `ForceRunScheduledJob`

### Dependent Jobs (ExecuteWhen)

```csharp
// Conditional execution for jobs triggered by ExecuteWhen
.ExecuteWhen(success: s => s
    .RunJob<EmailJob>().OnlyIf(() => emailEnabled)
    .RunJob<SlackJob>().OnlyIf((IFeatureFlagService flags) => 
        flags.IsEnabled("slack-notifications")))
```

## Performance Considerations

- **Instantiation Savings**: Jobs are never created when conditions fail, saving CPU and memory
- **Dependency Resolution**: Dependencies for conditions are resolved in a separate scope
- **Scope Lifetime**: The condition evaluation scope is disposed immediately after evaluation
- **Parallel Evaluation**: Multiple job conditions can be evaluated concurrently
- **No Lock Contention**: Conditions don't affect job concurrency settings

## Limitations

- Conditions cannot access the `IJobExecutionContext` (job hasn't been created yet)
- Conditions cannot be changed at runtime without restarting the application
- Delegate-based (untyped) jobs can use conditions, but condition handlers only work with typed jobs

## See Also

- [Feature Flags](https://martinfowler.com/articles/feature-toggles.html)
- [Retry Support](./retry-support.md)
- [Notifications](./notifications.md)
- [Concurrency Control](./concurrency-control.md)
