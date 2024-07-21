using NCronJob;

namespace NCronJobSample;

[RetryPolicy(retryCount: 4)]
public class TestRetryJob(ILogger<TestRetryJob> logger, int maxFailuresBeforeSuccess = 3)
    : IJob
{

    /// <summary>
    /// Runs the job, simulating failures based on a retry count. Will fail 3 times and then succeed.
    /// </summary>
    public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attemptCount = context.Attempts;

        if (attemptCount <= maxFailuresBeforeSuccess)
        {
            logger.LogWarning("TestRetryJob simulating failure.");
            throw new InvalidOperationException("Simulated operation failure in TestRetryJob.");
        }

        await Task.Delay(3000, token);
        logger.LogInformation($"TestRetryJob with instance Id {context.Id} completed successfully on attempt {attemptCount}.");
        await Task.CompletedTask;
    }
}

