using LinkDotNet.NCronJob.Messaging.States;
using LinkDotNet.NCronJob.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob.SignalR;

internal sealed class BufferedSignalRNotifier : IDisposable
{
    private readonly HubConnection hubConnection;
    private readonly List<JobDetailsView> jobDetailsBuffer = new();
    private readonly Timer timer;
    private readonly SemaphoreSlim asyncLock = new(1, 1);
    private readonly ILogger<BufferedSignalRNotifier> logger;
    private readonly IMonitorStateCronJobs jobService;
    private readonly Func<JobStateMessage, Task>? jobStatusChangedHandler;

    public BufferedSignalRNotifier(Lazy<HubConnection> hubConnection,
        ILogger<BufferedSignalRNotifier> logger,
        IMonitorStateCronJobs jobService)
    {
        this.hubConnection = hubConnection.Value;
        this.logger = logger;
        timer = new Timer(async _ => await FlushMessagesAsync(), null, Timeout.Infinite, Timeout.Infinite);

        this.jobService = jobService;
        jobStatusChangedHandler = JobStatusChangedHandler;

        AttachJobStatusChangedHandler();
    }

    private async Task JobStatusChangedHandler(JobStateMessage e)
    {
        await HandleJobStatusChangedAsync(e);
    }

    private void AttachJobStatusChangedHandler()
    {
        jobService.JobStatusChanged += jobStatusChangedHandler;
    }

    private void DetachJobStatusChangedHandler()
    {
        jobService.JobStatusChanged -= jobStatusChangedHandler;
    }

    private async Task HandleJobStatusChangedAsync(JobStateMessage e)
    {
        try
        {
            logger.LogTrace("[BufferedSignalRNotifier] Job: {JobName}", e.JobContext.JobType.FullName);

            var jobDetails = new JobDetailsView(e.JobContext);

            if (hubConnection.State != HubConnectionState.Connected)
            {
                logger.LogWarning("The SignalR connection is not open. [BufferedSignalRNotifier] Job: {JobName}", e.JobContext.JobType.FullName);
                return; // nothing to do since the SignalR is not connected
            }

            await asyncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                jobDetailsBuffer.Add(jobDetails);

                if (jobDetailsBuffer.Count == 1)
                {
                    timer.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
                }
            }
            finally
            {
                asyncLock.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while buffering a SignalR message");
        }
    }

    private async Task FlushMessagesAsync()
    {
        await asyncLock.WaitAsync();
        List<JobDetailsView> messagesToSend;

        try
        {
            if (jobDetailsBuffer.Count == 0)
                return;

            messagesToSend = jobDetailsBuffer[..];
            jobDetailsBuffer.Clear();
        }
        finally
        {
            asyncLock.Release();
        }

        if (hubConnection.State == HubConnectionState.Connected)
        {
            try
            {
                await hubConnection.SendAsync("ReceiveJobDetailsInformationBatch", messagesToSend);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while sending batched SignalR messages");
            }
        }
    }

    public void Dispose()
    {
        timer?.Dispose();
        asyncLock?.Dispose();
        DetachJobStatusChangedHandler();
    }
}

