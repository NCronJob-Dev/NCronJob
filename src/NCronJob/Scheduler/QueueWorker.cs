using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal sealed partial class QueueWorker : BackgroundService
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobWorker jobWorker;
    private readonly JobRegistry jobRegistry;
    private readonly ILogger<QueueWorker> logger;
    private readonly MissingMethodCalledHandler missingMethodCalledHandler;
    private CancellationTokenSource? shutdown;
    private readonly ConcurrentDictionary<string, Task?> workerTasks = new();
    private readonly ConcurrentDictionary<string, bool> addingWorkerTasks = new();
    private volatile bool isDisposed;

    public QueueWorker(
        JobQueueManager jobQueueManager,
        JobWorker jobWorker,
        JobRegistry jobRegistry,
        ILogger<QueueWorker> logger,
        MissingMethodCalledHandler missingMethodCalledHandler,
        IHostApplicationLifetime lifetime)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobWorker = jobWorker;
        this.jobRegistry = jobRegistry;
        this.logger = logger;
        this.missingMethodCalledHandler = missingMethodCalledHandler;

        lifetime.ApplicationStopping.Register(() => shutdown?.Cancel());

        this.jobQueueManager.CollectionChanged += HandleUpdate;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (isDisposed)
        {
            return;
        }
        
        if (shutdown is not null)
        {
            await shutdown.CancelAsync();
        }

        while (!workerTasks.IsEmpty)
        {
            var currentTasks = workerTasks.ToArray();

            foreach (var (jobType, task) in currentTasks)
            {
                if (task is null)
                {
                    continue;
                }

                var taskEnded = task.IsCanceled || task.IsFaulted || task.IsCompleted;
                if (taskEnded && workerTasks.TryRemove(jobType, out _))
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

    public override void Dispose()
    {
        if (isDisposed)
            return;

        shutdown?.Dispose();
        jobQueueManager.CollectionChanged -= HandleUpdate;
        jobQueueManager.QueueAdded -= OnQueueAdded;
        base.Dispose();
        isDisposed = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AssertUseNCronJobWasCalled();

        shutdown?.Dispose();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);

        try
        {
            ScheduleInitialJobs();

            CreateWorkerQueues(stopToken);
            jobQueueManager.QueueAdded += OnQueueAdded;  // this needs to come after we create the initial Worker Queues

            var tasks = workerTasks.Values.WhereNotNull();
            await Task.WhenAll(tasks).ConfigureAwait(false);
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

    private void AssertUseNCronJobWasCalled()
    {
        if (missingMethodCalledHandler.UseWasCalled)
        {
            return;
        }

        if (jobRegistry.GetAllOneTimeJobs().Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"""
            Startup jobs have been registered. However, neither IHost.UseNCronJobAsync(), nor IHost.UseNCronJob() have been been called.
            """);
    }

    private void CreateWorkerQueues(CancellationToken stopToken)
    {
        foreach (var jobQueueName in jobQueueManager.GetAllJobQueueNames())
        {
            AddWorkerTask(jobQueueName, stopToken);
        }
    }

    private void AddWorkerTask(string jobQueueName, CancellationToken stopToken)
    {
        if (!workerTasks.ContainsKey(jobQueueName) && !addingWorkerTasks.GetOrAdd(jobQueueName, _ => false))
        {
            addingWorkerTasks[jobQueueName] = true;
            try
            {
                var workerTask = jobWorker.WorkerAsync(jobQueueName, stopToken);
                workerTasks.TryAdd(jobQueueName, workerTask);

                workerTask.ContinueWith(_ =>
                {
                    addingWorkerTasks.TryUpdate(jobQueueName, false, true);
                }, stopToken, TaskContinuationOptions.None, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                LogQueueWorkerCreationError(jobQueueName, ex);
                addingWorkerTasks[jobQueueName] = false;
            }
        }
    }

    private void ScheduleInitialJobs()
    {
        foreach (var job in jobRegistry.GetAllCronJobs().Where(j => j.IsEnabled))
        {
            jobWorker.ScheduleJob(job);
        }
    }

    private void OnQueueAdded(string jobType)
    {
        AddWorkerTask(jobType, shutdown?.Token ?? CancellationToken.None);
        LogNewQueueAdded(jobType);
    }

    private void HandleUpdate(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
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
                throw new ArgumentOutOfRangeException(nameof(e), e.Action, $"Unexpected collection change action in {nameof(HandleUpdate)}");
        }
    }
}
