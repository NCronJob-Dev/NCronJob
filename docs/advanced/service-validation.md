# Service Validation

**NCronJob** provides a service validation feature that helps catch configuration errors early by checking if all jobs and their dependencies are properly registered in the service container before the application starts.

## Overview

When `ValidateOnBuild` is enabled, **NCronJob** will validate that:
- All registered jobs can be resolved from the service container
- All dependencies required by job constructors are registered
- All dependencies required by delegate-based jobs are registered

This is similar to ASP.NET Core's `ValidateOnBuild` option and helps prevent runtime errors caused by missing service registrations.

## Enabling Validation

You can enable validation by passing an options configuration to `UseNCronJobAsync`:

```csharp
var app = builder.Build();

await app.UseNCronJobAsync(options => 
{
    options.ValidateOnBuild = true;
});

await app.RunAsync();
```

## Automatic Validation in Development

By default, **NCronJob** automatically enables validation when running in a Development environment. This behavior aligns with ASP.NET Core's philosophy of catching errors early during development.

```csharp
var app = builder.Build();

// Validation is automatically enabled if IHostEnvironment.IsDevelopment() is true
await app.UseNCronJobAsync();

await app.RunAsync();
```

You can explicitly disable validation even in development mode:

```csharp
await app.UseNCronJobAsync(options => 
{
    options.ValidateOnBuild = false;
});
```

## Example: Missing Dependency Detection

Consider the following job that requires a service:

```csharp
public class EmailNotificationJob : IJob
{
    private readonly IEmailService emailService;

    public EmailNotificationJob(IEmailService emailService)
    {
        this.emailService = emailService;
    }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        await emailService.SendEmailAsync("Job completed!", token);
    }
}
```

If you register the job but forget to register `IEmailService`:

```csharp
builder.Services.AddNCronJob(options => 
{
    options.AddJob<EmailNotificationJob>(j => j.WithCronExpression("0 * * * *"));
});
// Oops! Forgot to register IEmailService
```

When validation is enabled, **NCronJob** will throw an exception during startup with a clear error message:

```
NCronJob service validation failed. The following issues were detected:
  1. Job 'EmailNotificationJob' has a dependency on 'IEmailService' which is not registered in the service container.
```

## Validation for Delegate Jobs

Validation also works for delegate-based (minimal API) jobs:

```csharp
builder.Services.AddNCronJob((IEmailService emailService) =>
{
    await emailService.SendEmailAsync("Hello from delegate job!");
}, "0 * * * *");

// If IEmailService is not registered, validation will catch it
```

## Best Practices

1. **Enable in Development**: Keep automatic validation enabled during development to catch configuration issues early.

2. **Consider in Production**: While validation adds a small startup cost, it can prevent runtime failures in production. Evaluate based on your deployment strategy.

3. **Fix Issues Early**: When validation fails, fix the underlying issue rather than disabling validation.

4. **Test Your Configuration**: Write integration tests that verify your job configurations with validation enabled.

## What Gets Validated

✅ **Validated**:
- Job type registrations
- Constructor dependencies for typed jobs
- Method parameter dependencies for delegate jobs

❌ **Not Validated**:
- Special parameters like `CancellationToken` and `IJobExecutionContext`
- Optional parameters with default values
- Runtime service resolution (services created during job execution)

## Performance Considerations

Validation is performed once during application startup by:
1. Creating a service scope
2. Attempting to resolve each job and its dependencies
3. Throwing an exception if any resolution fails

The performance impact is minimal and only occurs during startup, not during job execution.
