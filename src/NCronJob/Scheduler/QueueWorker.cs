using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace NCronJob;

internal sealed partial class QueueWorker : BackgroundService
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobWorker jobWorker;
    private readonly JobProcessor jobProcessor;
    private readonly JobRegistry registry;
    private readonly StartupJobManager startupJobManager;
    private readonly ILogger<QueueWorker> logger;
    private CancellationTokenSource? shutdown;
    private readonly ConcurrentDictionary<string, Task> workerTasks = new();

    public QueueWorker(
        JobQueueManager jobQueueManager,
        JobWorker jobWorker,
        JobProcessor jobProcessor,
        JobRegistry registry,
        StartupJobManager startupJobManager,
        ILogger<QueueWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobWorker = jobWorker;
        this.jobProcessor = jobProcessor;
        this.registry = registry;
        this.startupJobManager = startupJobManager;
        this.logger = logger;

        lifetime.ApplicationStopping.Register(() => shutdown?.Cancel());

        // Subscribe to CollectionChanged and QueueAdded events
        this.jobQueueManager.CollectionChanged += JobQueueManager_CollectionChanged;
        this.jobQueueManager.QueueAdded += OnQueueAdded;
    }

    public override void Dispose()
    {
        shutdown?.Dispose();
        this.jobQueueManager.CollectionChanged -= JobQueueManager_CollectionChanged;
        this.jobQueueManager.QueueAdded -= OnQueueAdded;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        shutdown?.Dispose();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);

        await startupJobManager.ProcessStartupJobs(CreateExecutionTask, stopToken);
        ScheduleInitialJobs();
        await startupJobManager.WaitForStartupJobsCompletion();

        CreateWorkerQueues(stopToken);

        await Task.WhenAll(workerTasks.Values).ConfigureAwait(false);
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
        if (!workerTasks.ContainsKey(jobType))
        {
            var workerTask = Task.Run(() => jobWorker.WorkerAsync(jobType, stopToken), stopToken);
            workerTasks.TryAdd(jobType, workerTask);
        }
    }

    public async Task CreateExecutionTask(JobRun job, CancellationToken stopToken)
    {
        using var semaphore = new SemaphoreSlim(1);
        await jobProcessor.ProcessJobAsync(job, semaphore, stopToken);
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

    private void JobQueueManager_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (JobRun job in e.NewItems!)
                {
                    LogJobAddedToQueue(job.JobDefinition.Type.Name, job.RunAt);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (JobRun job in e.OldItems!)
                {
                    LogJobRemovedFromQueue(job.JobDefinition.Type.Name, job.RunAt);
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
