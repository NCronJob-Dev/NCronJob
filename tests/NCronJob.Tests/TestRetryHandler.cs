using System.Reflection;
using System.Threading.Channels;
using Polly;

namespace NCronJob.Tests;

internal sealed class TestRetryHandler(
    IServiceProvider serviceProvider,
    ChannelWriter<object> writer,
    TaskCompletionSource<bool> cancellationHandled)
    : IRetryHandler
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, JobExecutionContext runContext, CancellationToken cancellationToken)
    {
        try
        {
            var retryPolicyAttribute = runContext.JobType.GetCustomAttribute<RetryPolicyBaseAttribute>();
            var retryPolicy = retryPolicyAttribute?.CreatePolicy(serviceProvider) ?? Policy.NoOpAsync();

            // Execute the operation using the given retry policy
            await retryPolicy.ExecuteAsync((ct) =>
            {
                runContext.Attempts++;
                return operation(ct);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancellationHandled.TrySetResult(true);
            await writer.WriteAsync("Job was canceled", CancellationToken.None);
        }
    }

}
