using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class QueueWorker : BackgroundService
{
    private readonly JobQueueManager jobQueueManager;
    private readonly JobWorker jobWorker;
    private readonly CronRunScheduler cronRunScheduler;
    private readonly ILogger<QueueWorker> logger;
    private readonly MissingMethodCalledHandler missingMethodCalledHandler;
    private readonly WorkerTaskRegistry workerTasks = new();
    private CancellationTokenSource? shutdown;
    private volatile bool isStopping;
    private int isDisposed;

    public QueueWorker(
        JobQueueManager jobQueueManager,
        JobWorker jobWorker,
        CronRunScheduler cronRunScheduler,
        ILogger<QueueWorker> logger,
        MissingMethodCalledHandler missingMethodCalledHandler,
        IHostApplicationLifetime lifetime)
    {
        this.jobQueueManager = jobQueueManager;
        this.jobWorker = jobWorker;
        this.cronRunScheduler = cronRunScheduler;
        this.logger = logger;
        this.missingMethodCalledHandler = missingMethodCalledHandler;

        lifetime.ApplicationStopping.Register(() => shutdown?.Cancel());
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref isDisposed) == 1)
        {
            return;
        }

        isStopping = true;

        if (shutdown is not null)
        {
            await shutdown.CancelAsync();
        }

        LogQueueWorkerDraining();

        await Task.WhenAll(workerTasks.GetTasks()).WaitAsync(cancellationToken);
        await jobWorker.WaitForRunningJobsAsync().WaitAsync(cancellationToken);

        LogQueueWorkerStopping();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) == 1)
        {
            return;
        }

        workerTasks.Close();

        shutdown?.Dispose();
        jobQueueManager.QueueAdded -= OnQueueAdded;

        base.Dispose();
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        missingMethodCalledHandler.EnsureUseWasCalledWhenStartupJobsExist();

        shutdown?.Dispose();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopToken = shutdown.Token;
        stopToken.Register(LogCancellationRequestedInJob);

        try
        {
            jobQueueManager.QueueAdded += OnQueueAdded;

            cronRunScheduler.ScheduleInitialRuns();

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
            await Task.WhenAll(workerTasks.GetTasks()).ConfigureAwait(false);
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

    internal IReadOnlyCollection<string> GetActiveWorkerQueueNames() => workerTasks.GetQueueNames();

    internal Task WaitForWorkerRemovalAsync(string queueName, CancellationToken cancellationToken) =>
        workerTasks.WaitForRemovalAsync(queueName, cancellationToken);

    private void CreateWorkerQueues(CancellationToken stopToken)
    {
        foreach (var jobQueueName in jobQueueManager.GetAllJobQueueNames())
        {
            AddWorkerTask(jobQueueName, stopToken);
        }
    }

    private void AddWorkerTask(string jobQueueName, CancellationToken stopToken)
    {
        try
        {
            if (!workerTasks.TryStart(
                    jobQueueName,
                    (Worker: this, QueueName: jobQueueName, StopToken: stopToken),
                    static state => state.Worker.isStopping || state.StopToken.IsCancellationRequested,
                    static state => state.Worker.StartWorker(state.QueueName, state.StopToken),
                    out var workerTask))
            {
                return;
            }

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

    private Task StartWorker(string jobQueueName, CancellationToken stopToken)
    {
        Task workerTask;

        // Workers may be created from within a running job, for example when it enqueues a dependent job.
        // They must not capture that job's execution context and its log scope.
        using (ExecutionContext.SuppressFlow())
        {
            workerTask = jobWorker.ProcessQueueAsync(jobQueueName, stopToken);
        }

        return workerTask;
    }

    private void OnWorkerCompleted(string jobQueueName, Task completedTask, CancellationToken stopToken)
    {
        if (completedTask.IsCanceled)
            LogJobQueueCancelled(jobQueueName);
        else if (completedTask.IsFaulted)
            LogJobQueueFaulted(jobQueueName, completedTask.Exception);
        else
            LogJobQueueCompleted(jobQueueName);

        workerTasks.Remove(jobQueueName, completedTask);

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

    private void OnQueueAdded(string queueName)
    {
        AddWorkerTask(queueName, shutdown?.Token ?? CancellationToken.None);
        LogNewQueueAdded(queueName);
    }
}
