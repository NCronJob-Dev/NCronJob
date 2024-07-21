# Concurrency Control

Concurrency support allows multiple instances of the same job type to run simultaneously, controlled by the `SupportsConcurrency` attribute. This feature is crucial for efficiently managing jobs that are capable of running in parallel without interference.
By default jobs are not executed concurrently if the `SupportsConcurrency` attribute is not set.

## How It Works

The `SupportsConcurrency` attribute specifies the maximum degree of parallelism for job instances. This means you can define how many instances of a particular job can run concurrently, optimizing performance and resource utilization based on the nature of the job and the system capabilities.

## Using the SupportsConcurrency Attribute

Here is an example of how to apply this attribute to a job:

### Example: Concurrency in Jobs

```csharp
[SupportsConcurrency(10)]
public class ConcurrentJob : IJob
{
    private readonly ILogger<ConcurrentJob> logger;

    public ConcurrentJob(ILogger<ConcurrentJob> logger)
    {
        this.logger = logger;
    }

    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        logger.LogInformation($"ConcurrentJob with Id {context.Id} is running.");
        // Simulate some work by delaying
        await Task.Delay(5000, token);
        logger.LogInformation($"ConcurrentJob with Id {context.Id} has completed.");
    }
}
```

## Important Considerations

### Ensuring Job Idempotency
When using concurrency, it's essential to ensure that each job instance is idempotent. This means that even if the job is executed multiple times concurrently or sequentially, the outcome and side effects should remain consistent, without unintended duplication or conflict.

### Resource Allocation Caution
Jobs that are marked to support concurrency should be designed carefully to avoid contention over shared resources. This includes, but is not limited to, database connections, file handles, or any external systems. In scenarios where shared resources are unavoidable, proper synchronization mechanisms or concurrency control techniques, such as semaphores, mutexes, or transactional control, should be implemented to prevent race conditions and ensure data integrity.
