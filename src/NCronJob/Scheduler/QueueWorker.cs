using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;

namespace NCronJob;

internal sealed partial class QueueWorker : BackgroundService
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobWorker jobWorker;
    private readonly JobRegistry jobRegistry;
    private readonly ILogger<QueueWorker> logger;
    private readonly MissingMethodCalledHandler missingMethodCalledHandler;
    private CancellationTokenSource? shutdown;
    private readonly Dictionary<string, Task> workerTasks = [];
#if NET9_0_OR_GREATER
    private readonly Lock workerTasksLock = new();
#else
    private readonly object workerTasksLock = new();
#endif
    private volatile bool isStopping;
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

        isStopping = true;

        if (shutdown is not null)
        {
            await shutdown.CancelAsync();
        }

        LogQueueWorkerDraining();

        await Task.WhenAll(GetWorkerTasksSnapshot()).WaitAsync(cancellationToken);
        await jobWorker.WaitForRunningJobsAsync().WaitAsync(cancellationToken);

        LogQueueWorkerStopping();
        await base.StopAsync(cancellationToken);
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

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        AssertUseNCronJobWasCalled();

        shutdown?.Dispose();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);

        try
        {
            jobQueueManager.QueueAdded += OnQueueAdded;

            ScheduleInitialJobs();

            CreateWorkerQueues(stopToken);
        }
        catch (Exception ex)
        {
            LogQueueWorkerError(ex);
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.WhenAll(GetWorkerTasksSnapshot()).ConfigureAwait(false);
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
        lock (workerTasksLock)
        {
            if (isStopping || stopToken.IsCancellationRequested || workerTasks.ContainsKey(jobQueueName))
            {
                return;
            }

            try
            {
                Task workerTask;

                // Workers may be created from within a running job, for example when it enqueues a dependent job.
                // They must not capture that job's execution context and its log scope.
                using (ExecutionContext.SuppressFlow())
                {
                    workerTask = jobWorker.WorkerAsync(jobQueueName, stopToken);
                }

                workerTasks[jobQueueName] = workerTask;

                workerTask.ContinueWith(
                    completedTask => OnWorkerCompleted(jobQueueName, completedTask, stopToken),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                LogQueueWorkerCreationError(jobQueueName, ex);
            }
        }
    }

    private void OnWorkerCompleted(string jobQueueName, Task completedTask, CancellationToken stopToken)
    {
        if (completedTask.IsCanceled)
            LogJobQueueCancelled(jobQueueName);
        else if (completedTask.IsFaulted)
            LogJobQueueFaulted(jobQueueName, completedTask.Exception);
        else
            LogJobQueueCompleted(jobQueueName);

        lock (workerTasksLock)
        {
            if (workerTasks.TryGetValue(jobQueueName, out var currentTask) && currentTask == completedTask)
            {
                workerTasks.Remove(jobQueueName);
            }
        }

        try
        {
            // The queue may have been removed and re-created while the previous worker was exiting.
            if (jobQueueManager.TryGetQueue(jobQueueName, out _))
            {
                AddWorkerTask(jobQueueName, stopToken);
            }
        }
        catch (ObjectDisposedException) when (jobQueueManager.IsDisposed)
        {
            LogJobQueueCompleted(jobQueueName);
        }
    }

    internal IReadOnlyCollection<string> GetActiveWorkerQueueNames()
    {
        lock (workerTasksLock)
        {
            return [.. workerTasks.Keys];
        }
    }

    private Task[] GetWorkerTasksSnapshot()
    {
        lock (workerTasksLock)
        {
            return [.. workerTasks.Values];
        }
    }

    private void ScheduleInitialJobs()
    {
        foreach (var job in jobRegistry.GetAllCronJobs())
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
