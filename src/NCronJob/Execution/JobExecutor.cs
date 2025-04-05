using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutor : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<JobExecutor> logger;
    private readonly IRetryHandler retryHandler;
    private readonly JobQueueManager jobQueueManager;
    private readonly JobRegistry jobRegistry;
    private readonly ImmutableArray<IExceptionHandler> exceptionHandlers;
    private volatile bool isDisposed;
    private readonly CancellationTokenSource shutdown = new();

    public JobExecutor(
        IServiceProvider serviceProvider,
        ILogger<JobExecutor> logger,
        IHostApplicationLifetime lifetime,
        IRetryHandler retryHandler,
        JobQueueManager jobQueueManager,
        JobRegistry jobRegistry,
        IEnumerable<IExceptionHandler> exceptionHandlers)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.retryHandler = retryHandler;
        this.jobQueueManager = jobQueueManager;
        this.jobRegistry = jobRegistry;
        this.exceptionHandlers = [.. exceptionHandlers];

        lifetime.ApplicationStopping.Register(OnApplicationStopping);
    }

    public void CancelJobs()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (!shutdown.IsCancellationRequested)
        {
            shutdown.Cancel();
        }
    }

    public async Task RunJob(JobRun run, CancellationToken stoppingToken)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (isDisposed)
        {
            LogSkipAsDisposed();
            return;
        }

        // stoppingToken is never cancelled when the job is triggered outside the BackgroundProcess,
        // so we need to tie into the IHostApplicationLifetime
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, stoppingToken, run.CancellationToken);
        run.CancellationToken = linkedCts.Token;

        await using var scope = serviceProvider.CreateAsyncScope();
        var runContext = new JobExecutionContext(run);

        try
        {
            var job = ResolveJob(scope.ServiceProvider, run.JobDefinition);
            await ExecuteJob(runContext, job);
        }
        catch (Exception exc) when (exc is not OperationCanceledException or AggregateException)
        {
            LogJobFailed(runContext.JobRun.JobDefinition.Name, runContext.CorrelationId);
            await NotifyExceptionHandlers(runContext, exc, stoppingToken);
            await AfterJobCompletionTask(runContext, exc, linkedCts.Token);
            runContext.JobRun.NotifyStateChange(JobStateType.Faulted, exc);
        }
    }

    private IJob ResolveJob(IServiceProvider scopedServiceProvider, JobDefinition definition)
    {
        var job = definition.ResolveJob(scopedServiceProvider);
        if (job != null)
        {
            return job;
        }

        Debug.Assert(definition.IsTypedJob);

        LogUnregisteredJob(definition.Name);

        return (IJob)ActivatorUtilities.CreateInstance(scopedServiceProvider, definition.Type);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        shutdown.Dispose();
        isDisposed = true;
    }

    private async Task ExecuteJob(JobExecutionContext runContext, IJob job)
    {
        var stoppingToken = runContext.JobRun.CancellationToken;
        if (!runContext.JobRun.CanRun)
        {
            return;
        }

        runContext.JobRun.NotifyStateChange(JobStateType.Running);
        LogRunningJob(runContext.JobRun.JobDefinition.Name, runContext.CorrelationId);

        await retryHandler.ExecuteAsync(async token => await job.RunAsync(runContext, token), runContext, stoppingToken);

        stoppingToken.ThrowIfCancellationRequested();

        runContext.JobRun.NotifyStateChange(JobStateType.Completing);

        await AfterJobCompletionTask(runContext, null, stoppingToken);
    }

    private async Task AfterJobCompletionTask(
        JobExecutionContext runContext,
        Exception? exc,
        CancellationToken ct)
    {
        if (isDisposed)
        {
            LogSkipAsDisposed();
            return;
        }

        await TriggerNotifications(runContext, exc, ct);

        InformDependentJobs(runContext, exc is null);
    }

    private async Task TriggerNotifications(JobExecutionContext runContext, Exception? exc, CancellationToken ct)
    {
        if (!runContext.JobRun.JobDefinition.IsTypedJob)
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var notificationServiceType = typeof(IJobNotificationHandler<>).MakeGenericType(runContext.JobRun.JobDefinition.Type);

        if (scope.ServiceProvider.GetService(notificationServiceType) is IJobNotificationHandler notificationService)
        {
            try
            {
                await notificationService.HandleAsync(runContext, exc, ct).ConfigureAwait(false);
            }
            catch (Exception innerExc) when (innerExc is not OperationCanceledException or AggregateException)
            {
                // We don't want to throw exceptions from the notification service
            }
        }
    }

    public void InformDependentJobs(JobExecutionContext context, bool success)
    {
        var jobRun = context.JobRun;
        var dependencies = success
            ? jobRegistry.GetDependentSuccessJobTypes(jobRun.JobDefinition)
            : jobRegistry.GetDependentFaultedJobTypes(jobRun.JobDefinition);

        if (dependencies.Count > 0)
            jobRun.NotifyStateChange(JobStateType.WaitingForDependency);

        foreach (var dependentJob in dependencies)
        {
            var newRun = jobRun.CreateDependent(
                dependentJob,
                dependentJob.Parameter,
                jobRun.CancellationToken);

            newRun.ParentOutput = context.Output;

            if (!context.ExecuteChildren)
            {
                newRun.NotifyStateChange(JobStateType.Skipped);
                continue;
            }

            var jobQueue = jobQueueManager.GetOrAddQueue(newRun.JobDefinition.JobFullName);
            jobQueue.EnqueueForDirectExecution(newRun);
        }
    }

    private async Task NotifyExceptionHandlers(JobExecutionContext runContext, Exception exc,
        CancellationToken stoppingToken)
    {
        foreach (var exceptionHandler in exceptionHandlers)
        {
            try
            {
                if (await exceptionHandler.TryHandleAsync(runContext, exc, stoppingToken))
                {
                    break;
                }
            }
            catch
            {
                LogExceptionHandlerError(exceptionHandler.GetType());
            }
        }
    }

    private void OnApplicationStopping()
    {
        CancelJobs();
        Dispose();
    }
}
