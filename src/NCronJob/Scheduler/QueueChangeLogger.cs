using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;

namespace NCronJob;

/// <summary>
/// Logs every job that is added to or removed from a queue.
/// </summary>
/// <remarks>
/// Subscribes on construction so no change is missed, regardless of the order hosted services are started in.
/// </remarks>
internal sealed partial class QueueChangeLogger : IHostedService, IDisposable
{
    private readonly JobQueueManager jobQueueManager;
    private readonly ILogger<QueueChangeLogger> logger;

    public QueueChangeLogger(JobQueueManager jobQueueManager, ILogger<QueueChangeLogger> logger)
    {
        this.jobQueueManager = jobQueueManager;
        this.logger = logger;

        this.jobQueueManager.CollectionChanged += LogQueueChange;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => jobQueueManager.CollectionChanged -= LogQueueChange;

    private void LogQueueChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove when !logger.IsEnabled(LogLevel.Debug):
                break;
            case NotifyCollectionChangedAction.Add:
                foreach (JobRun job in e.NewItems!)
                {
                    LogJobAddedToQueue(job.JobDefinition.Name, job.RunAt);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (JobRun job in e.OldItems!)
                {
                    LogJobRemovedFromQueue(job.JobDefinition.Name, job.RunAt);
                }
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
            case NotifyCollectionChangedAction.Reset:
            default:
                throw new ArgumentOutOfRangeException(nameof(e), e.Action, $"Unexpected collection change action in {nameof(LogQueueChange)}");
        }
    }

    [LoggerMessage(LogLevel.Debug, "Job added to queue: '{JobName}' at {RunAt:o}")]
    private partial void LogJobAddedToQueue(string jobName, DateTimeOffset? runAt);

    [LoggerMessage(LogLevel.Debug, "Job removed from queue: '{JobName}' at {RunAt:o}")]
    private partial void LogJobRemovedFromQueue(string jobName, DateTimeOffset? runAt);
}
