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
    private readonly JobResolver jobResolver;
    private readonly DependentJobDispatcher dependentJobDispatcher;
    private readonly ExceptionHandlerPipeline exceptionHandlerPipeline;
    private readonly TimeProvider timeProvider;
    private volatile bool isDisposed;
    private readonly CancellationTokenSource shutdown = new();

    public JobExecutor(
        IServiceProvider serviceProvider,
        ILogger<JobExecutor> logger,
        IHostApplicationLifetime lifetime,
        IRetryHandler retryHandler,
        JobResolver jobResolver,
        DependentJobDispatcher dependentJobDispatcher,
        ExceptionHandlerPipeline exceptionHandlerPipeline,
        TimeProvider timeProvider)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.retryHandler = retryHandler;
        this.jobResolver = jobResolver;
        this.dependentJobDispatcher = dependentJobDispatcher;
        this.exceptionHandlerPipeline = exceptionHandlerPipeline;
        this.timeProvider = timeProvider;

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

        // stoppingToken is never cancelled when the job is triggered outside the BackgroundProcess,
        // so we need to tie into the IHostApplicationLifetime
        using var timeoutCts = run.JobDefinition.Timeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(run.JobDefinition.Timeout, timeProvider);
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, stoppingToken, run.CancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, stoppingToken, run.CancellationToken, timeoutCts.Token);
        run.CancellationToken = linkedCts.Token;

        await using var scope = serviceProvider.CreateAsyncScope();
        var runContext = JobExecutionContext.Create(run);
        using var activity = StartActivity(runContext);

        try
        {
            var job = jobResolver.Resolve(scope.ServiceProvider, run.JobDefinition);
            await ExecuteJob(runContext, job);
        }
        catch (Exception exc) when (!IsRequestedCancellation(exc, linkedCts.Token))
        {
            activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
            activity?.SetTag("error.type", exc.GetType().FullName);
            LogJobFailed(runContext.JobRun.JobDefinition.Name, runContext.CorrelationId);
            await exceptionHandlerPipeline.HandleAsync(runContext, exc, stoppingToken);
            await AfterJobCompletionTask(runContext, exc, linkedCts.Token);
            runContext.JobRun.NotifyStateChange(JobStateType.Faulted, exc);
        }
        finally
        {
            activity?.SetTag("ncronjob.attempts", runContext.Attempts);
        }
    }

    private static Activity? StartActivity(JobExecutionContext runContext)
    {
        var activity = NCronJobDiagnostics.ActivitySource.StartActivity(runContext.JobRun.JobDefinition.Name);
        activity?
            .SetTag("ncronjob.job.name", runContext.JobRun.JobDefinition.Name)
            .SetTag("ncronjob.correlation_id", runContext.CorrelationId.ToString())
            .SetTag("ncronjob.trigger_type", runContext.TriggerType.ToString());
        return activity;
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

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await retryHandler.ExecuteAsync(token => job.RunAsync(runContext, token), runContext, stoppingToken);
        }
        finally
        {
            NCronJobMetrics.RecordDuration(runContext.JobRun.JobDefinition.Name, runContext.TriggerType, startTimestamp);
        }

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

        dependentJobDispatcher.Dispatch(runContext, exc is null);
    }

    private Task TriggerNotifications(JobExecutionContext runContext, Exception? exc, CancellationToken ct) =>
        TypedJobHandlerInvoker.InvokeNotificationHandlerAsync(
            serviceProvider,
            runContext.JobRun.JobDefinition,
            async notificationService =>
            {
                try
                {
                    await notificationService.HandleAsync(runContext, exc, ct).ConfigureAwait(false);
                }
                catch (Exception innerExc) when (!IsRequestedCancellation(innerExc, ct))
                {
                    LogNotificationHandlerFailed(notificationService.GetType(), innerExc);
                }
            });

    private static bool IsRequestedCancellation(Exception exception, CancellationToken token) =>
        exception is OperationCanceledException && token.IsCancellationRequested;

    private void OnApplicationStopping()
    {
        CancelJobs();
        Dispose();
    }
}
