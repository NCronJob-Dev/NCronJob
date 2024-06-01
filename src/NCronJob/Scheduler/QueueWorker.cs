using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace NCronJob;

internal sealed partial class QueueWorker : BackgroundService
{
    private readonly JobExecutor jobExecutor;
    private readonly JobRegistry registry;
    private readonly StartupJobManager startupJobManager;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<QueueWorker> logger;
    private readonly int globalConcurrencyLimit;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> semaphores;
    private readonly ConcurrentDictionary<string, JobQueue> jobQueues;
    private readonly ConcurrentDictionary<string, int> runningJobCounts = [];
    private readonly ConcurrentDictionary<string, Task> workerTasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> jobCancellationTokens = new();
    private CancellationTokenSource? shutdown;

    private int TotalRunningJobCount => runningJobCounts.Values.Sum();

    public QueueWorker(
        JobExecutor jobExecutor,
        JobRegistry registry,
        StartupJobManager startupJobManager,
        TimeProvider timeProvider,
        ConcurrencySettings concurrencySettings,
        ILogger<QueueWorker> logger,
        IHostApplicationLifetime lifetime,
        ConcurrentDictionary<string, JobQueue> jobQueues)
    {
        this.jobExecutor = jobExecutor;
        this.registry = registry;
        this.startupJobManager = startupJobManager;
        this.timeProvider = timeProvider;
        this.logger = logger;
        globalConcurrencyLimit = concurrencySettings.MaxDegreeOfParallelism;
        semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        this.jobQueues = jobQueues;
        this.startupJobManager = startupJobManager;

        lifetime.ApplicationStopping.Register(() => shutdown?.Cancel());
    }

    public override void Dispose()
    {
        shutdown?.Dispose();
        foreach (var semaphore in semaphores.Values)
        {
            semaphore.Dispose();
        }
        foreach (var cts in jobCancellationTokens.Values)
        {
            cts.Dispose();
        }
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

        foreach (var jobType in registry.GetAllJobTypes())
        {
            var workerTask = Task.Run(() => WorkerAsync(jobType, stopToken), stopToken);
            workerTasks.TryAdd(jobType, workerTask);
        }

        await Task.WhenAll(workerTasks.Values).ConfigureAwait(false);
    }

    private async Task WorkerAsync(string jobType, CancellationToken cancellationToken)
    {
        var jobQueue = jobQueues.GetOrAdd(jobType, _ => new JobQueue(timeProvider));
        var concurrencyLimit = registry.GetJobTypeConcurrencyLimit(jobType);
        var semaphore = semaphores.GetOrAdd(jobType, _ => new SemaphoreSlim(concurrencyLimit));
        var cts = new CancellationTokenSource();
        jobCancellationTokens[jobType] = cts;

        while (!cancellationToken.IsCancellationRequested)
        {
            await semaphore.WaitAsync(cancellationToken);

            if (jobQueue.TryPeek(out var nextJob, out var priorityTuple))
            {
                try
                {
                    // there must be a better way to handle this 🤔
                    lock (jobCancellationTokens)
                    {
                        if (cts.IsCancellationRequested || cts.Token.IsCancellationRequested)
                        {
#pragma warning disable S3966 // Suppress the warning for redundant disposal
                            cts.Dispose();
#pragma warning restore S3966
                            cts = new CancellationTokenSource();
                            jobCancellationTokens[jobType] = cts;
                        }
                    }
                    using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                    await WaitForNextExecution(priorityTuple, tokenSource.Token);

                    if (IsJobEligibleToStart(nextJob, jobQueue))
                    {
                        jobQueue.Dequeue();
                        _ = ProcessJobAsync(nextJob, semaphore, cancellationToken);
                        if (!nextJob.IsOneTimeJob)
                        {
                            ScheduleJob(nextJob);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    LogCancellationOperationInJob();
                    nextJob.NotifyStateChange(new JobState(JobStateType.Cancelled));
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Handle manual cancellation (instant job insertion)
                }
            }
            else
            {
                semaphore.Release();
                await Task.Delay(100, cancellationToken); // Avoid tight loop
            }
        }
    }

    private async Task ProcessJobAsync(JobDefinition job, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            if (job.IsExpired)
            {
                LogDequeuingExpiredJob(job.JobName);
                job.NotifyStateChange(new JobState(JobStateType.Cancelled));
                return;
            }

            job.NotifyStateChange(new JobState(JobStateType.Running));
            UpdateRunningJobCount(job.JobFullName, 1);

            await jobExecutor.RunJob(job, cancellationToken).ConfigureAwait(false);

            job.NotifyStateChange(new JobState(JobStateType.Completed));
        }
        catch (Exception ex)
        {
            job.NotifyStateChange(new JobState(JobStateType.Failed, ex.Message));
            LogExceptionInJob(ex.Message, job.Type);
        }
        finally
        {
            UpdateRunningJobCount(job.JobFullName, -1);
            semaphore.Release();
        }
    }

    private void ScheduleInitialJobs()
    {
        foreach (var job in registry.GetAllCronJobs())
        {
            ScheduleJob(job);
        }
    }

    private void ScheduleJob(JobDefinition job)
    {
        if (job.CronExpression is null)
            return;

        var utcNow = timeProvider.GetUtcNow();
        var nextRunTime = job.CronExpression!.GetNextOccurrence(utcNow, job.TimeZone);

        if (nextRunTime.HasValue)
        {
            job.NotifyStateChange(new JobState(JobStateType.Scheduled));
            var jobQueue = jobQueues.GetOrAdd(job.JobFullName, _ => new JobQueue(timeProvider));
            jobQueue.Enqueue(job, (nextRunTime.Value, (int)job.Priority));
        }
    }

    private async Task WaitForNextExecution((DateTimeOffset NextRunTime, int Priority) priorityTuple, CancellationToken stopToken)
    {
        var utcNow = timeProvider.GetUtcNow();
        var delay = priorityTuple.NextRunTime - utcNow;
        if (delay > TimeSpan.Zero)
        {
            await TaskExtensions.LongDelaySafe(delay, timeProvider, stopToken);
        }
    }

    public async Task CreateExecutionTask(JobDefinition job, CancellationToken stopToken)
    {
        using var semaphore = new SemaphoreSlim(1);
        await ProcessJobAsync(job, semaphore, stopToken);
    }

    public void SignalJobQueue(string jobType)
    {
        if (jobCancellationTokens.TryGetValue(jobType, out var cts))
        {
            cts.Cancel();
            if (!cts.IsCancellationRequested)
            {
                jobCancellationTokens[jobType] = new CancellationTokenSource();
            }
        }
    }

    private bool IsJobEligibleToStart(JobDefinition nextJob, ObservablePriorityQueue<JobDefinition> jobQueue)
    {
        var isSameJob = jobQueue.TryPeek(out var confirmedNextJob, out _) && confirmedNextJob == nextJob;
        var concurrentSlotsOpen = TotalRunningJobCount < globalConcurrencyLimit;
        return isSameJob && CanStartJob(nextJob) && concurrentSlotsOpen;
    }

    private bool CanStartJob(JobDefinition jobEntry)
    {
        var maxAllowed = jobEntry.ConcurrencyPolicy?.MaxDegreeOfParallelism ?? 1;
        var currentCount = runningJobCounts.GetOrAdd(jobEntry.JobFullName, _ => 0);

        return currentCount < maxAllowed;
    }

    private void UpdateRunningJobCount(string jobFullName, int change) =>
        runningJobCounts.AddOrUpdate(jobFullName, change, (_, existingVal) => Math.Max(0, existingVal + change));
}
