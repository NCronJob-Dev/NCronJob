using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace NCronJob;

#pragma warning disable CA2008
internal sealed partial class QueueWorker : BackgroundService
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobWorker jobWorker;
    private readonly JobRegistry registry;
    private readonly StartupJobManager startupJobManager;
    private readonly ILogger<QueueWorker> logger;
    private CancellationTokenSource? shutdown;
    private readonly ConcurrentDictionary<string, Task> workerTasks = new();
    private readonly ConcurrentDictionary<string, bool> addingWorkerTasks = new();
    private volatile bool isDisposed;

    public QueueWorker(
        JobQueueManager jobQueueManager,
        JobWorker jobWorker,
        JobRegistry registry,
        StartupJobManager startupJobManager,
        ILogger<QueueWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobWorker = jobWorker;
        this.registry = registry;
        this.startupJobManager = startupJobManager;
        this.logger = logger;

        lifetime.ApplicationStopping.Register(() => shutdown?.Cancel());

        // Subscribe to CollectionChanged and QueueAdded events
        this.jobQueueManager.CollectionChanged += JobQueueManager_CollectionChanged;
    }

    public override void Dispose()
    {
        if (isDisposed)
            return;

        shutdown?.Dispose();
        this.jobQueueManager.CollectionChanged -= JobQueueManager_CollectionChanged;
        this.jobQueueManager.QueueAdded -= OnQueueAdded;
        base.Dispose();
        isDisposed = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        shutdown?.Dispose();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);

        try
        {
            await startupJobManager.ProcessStartupJobs(stopToken).ConfigureAwait(false);
            ScheduleInitialJobs();
            await startupJobManager.WaitForStartupJobsCompletion().ConfigureAwait(false);

            CreateWorkerQueues(stopToken);
            jobQueueManager.QueueAdded += OnQueueAdded;  // this needs to come after we create the initial Worker Queues

            await Task.WhenAll(workerTasks.Values).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogQueueWorkerShuttingDown();
        }
        catch (Exception ex)
        {
            LogQueueWorkerError(ex);
        }
    }

    private void CreateWorkerQueues(CancellationToken stopToken)
    {
        foreach (var jobType in jobQueueManager.GetAllJobTypes())
        {
            AddWorkerTask(jobType, stopToken);
        }
    }

    private void AddWorkerTask(string jobType, CancellationToken stopToken)
    {
        if (!workerTasks.ContainsKey(jobType) && !addingWorkerTasks.GetOrAdd(jobType, _ => false))
        {
            addingWorkerTasks[jobType] = true;
            try
            {
                var workerTask = jobWorker.WorkerAsync(jobType, stopToken);
                workerTasks.TryAdd(jobType, workerTask);

                workerTask.ContinueWith(_ =>
                {
                    addingWorkerTasks.TryUpdate(jobType, false, true);
                }, stopToken);
            }
            catch (Exception ex)
            {
                LogQueueWorkerCreationError(jobType, ex);
                addingWorkerTasks[jobType] = false;
            }
        }
    }

    private void ScheduleInitialJobs()
    {
        foreach (var job in registry.GetAllCronJobs())
        {
            jobWorker.ScheduleJob(job);
        }
    }

    private void OnQueueAdded(string jobType)
    {
        AddWorkerTask(jobType, shutdown?.Token ?? CancellationToken.None);
        LogNewQueueAdded(jobType);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (shutdown != null)
        {
            await shutdown.CancelAsync();
        }

        while (workerTasks.IsEmpty)
        {
            var currentTasks = workerTasks.ToList();

            foreach (var kvp in currentTasks)
            {
                var jobType = kvp.Key;
                var task = kvp.Value;

                if ((task.IsCanceled || task.IsFaulted || task.IsCompleted) && workerTasks.TryRemove(jobType, out _))
                {
                    if (task.IsCanceled)
                        LogJobQueueCancelled(jobType);
                    else if (task.IsFaulted)
                        LogJobQueueFaulted(jobType);
                    else if (task.IsCompleted)
                        LogJobQueueCompleted(jobType);
                }
            }

            if (workerTasks.IsEmpty)
            {
                LogQueueWorkerStopping();
                await base.StopAsync(cancellationToken);
                break;
            }

            LogQueueWorkerDraining();
            await Task.Delay(500, cancellationToken);
        }
    }

    private void JobQueueManager_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (JobRun job in e.NewItems!)
                {
                    LogJobAddedToQueue(job.JobDefinition.Type.Name, job.RunAt?.LocalDateTime);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (JobRun job in e.OldItems!)
                {
                    LogJobRemovedFromQueue(job.JobDefinition.Type.Name, job.RunAt?.LocalDateTime);
                }
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
            case NotifyCollectionChangedAction.Reset:
            default:
                throw new ArgumentOutOfRangeException(nameof(e), e.Action, "Unexpected collection change action in JobQueueManager_CollectionChanged");
        }
    }
}
