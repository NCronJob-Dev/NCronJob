# Retry Support

The new Retry support provides a robust mechanism for handling transient failures by retrying failed operations. This feature is implemented using the `RetryPolicy` attribute that can be applied to any class implementing the `IJob` interface.

## How It Works

The `RetryPolicy` attribute allows you to specify the number of retry attempts and the strategy for handling retries. There are two built-in retry strategies:
- **ExponentialBackoff:** Increases the delay between retry attempts exponentially.
- **FixedInterval:** Keeps the delay between retry attempts consistent.

## Using Retry Policies

Here are examples of how to use the built-in retry policies:

### Example 1: Basic Retry Policy, defaults to Exponential Backoff

```csharp
[RetryPolicy(retryCount: 4)]
public class RetryJob(ILogger<RetryJob> logger) : IJob
{
    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        var attemptCount = context.Attempts;

        if (attemptCount <= 3)
        {
            logger.LogWarning("RetryJob simulating failure.");
            throw new InvalidOperationException("Simulated operation failure in RetryJob.");
        }

        logger.LogInformation($"RetryJob with Id {context.Id} was attempted {attemptCount} times.");
        await Task.CompletedTask;
    }
}
```

### Example 2: Fixed Interval

```csharp
[RetryPolicy(4, PolicyType.FixedInterval)]
public class FixedIntervalRetryJob(ILogger<FixedIntervalRetryJob> logger) : IJob
{
    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        var attemptCount = context.Attempts;

        if (attemptCount <= 3)
        {
            logger.LogWarning("FixedIntervalRetryJob simulating failure.");
            throw new InvalidOperationException("Simulated operation failure in FixedIntervalRetryJob.");
        }

        logger.LogInformation($"FixedIntervalRetryJob with Id {context.Id} was attempted {attemptCount} times.");
        await Task.CompletedTask;
    }
}
```

## Advanced: Custom Retry Policies

You can also create custom retry policies by implementing the `IPolicyCreator` interface. This allows you to define complex retry logic tailored to your specific needs.

```csharp
[RetryPolicy<MyCustomPolicyCreator>(retryCount:4, delayFactor:1)]
public class CustomPolicyJob(ILogger<CustomPolicyJob> logger) : IJob
{
    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        var attemptCount = context.Attempts;

        if (attemptCount <= 3)
        {
            logger.LogWarning("FixedIntervalRetryJob simulating failure.");
            throw new InvalidOperationException("Simulated operation failure in FixedIntervalRetryJob.");
        }

        logger.LogInformation($"CustomPolicyJob with Id {context.Id} was attempted {attemptCount} times.");
        await Task.CompletedTask;
    }
}

public class MyCustomPolicyCreator : IPolicyCreator
{
    public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2)
    {
        return Policy.Handle<Exception>()
            .WaitAndRetryAsync(maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(delayFactor, retryAttempt)));
    }
}
```
