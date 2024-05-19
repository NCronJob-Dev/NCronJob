using Microsoft.AspNetCore.SignalR.Client;

namespace LinkDotNet.NCronJob.Client;

public class RetryPolicyLoop : IRetryPolicy
{
    private const int ReconnectionWaitSeconds = 5;

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        return TimeSpan.FromSeconds(ReconnectionWaitSeconds);
    }
}
