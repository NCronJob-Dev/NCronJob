using System.Collections.Immutable;
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
    private readonly DynamicJobFactoryRegistry dynamicJobFactoryRegistry;
    private readonly IJobHistory jobHistory;
    private readonly DependingJobRegistry dependingJobRegistry;
    private readonly ImmutableArray<IExceptionHandler> exceptionHandlers;
    private volatile bool isDisposed;
    private readonly CancellationTokenSource shutdown = new();

    public JobExecutor(
        IServiceProvider serviceProvider,
        ILogger<JobExecutor> logger,
        IHostApplicationLifetime lifetime,
        IRetryHandler retryHandler,
        JobQueueManager jobQueueManager,
        DynamicJobFactoryRegistry dynamicJobFactoryRegistry,
        IJobHistory jobHistory,
        IEnumerable<IExceptionHandler> exceptionHandlers,
        DependingJobRegistry dependingJobRegistry)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.retryHandler = retryHandler;
        this.jobQueueManager = jobQueueManager;
        this.dynamicJobFactoryRegistry = dynamicJobFactoryRegistry;
        this.jobHistory = jobHistory;
        this.dependingJobRegistry = dependingJobRegistry;
        this.exceptionHandlers = [..exceptionHandlers];

        lifetime.ApplicationStopping.Register(OnApplicationStopping);
    }

    private void OnApplicationStopping()
    {
        CancelJobs();
        Dispose();
    }

    public void CancelJobs()
    {
        if (!shutdown.IsCancellationRequested)
        {
            shutdown.Cancel();
        }
    }

    public async Task RunJob(JobRun run, CancellationToken stoppingToken)
    {
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

        var job = ResolveJob(scope.ServiceProvider, run.JobDefinition);

        jobHistory.Add(run);
        var runContext = new JobExecutionContext(run);
        await ExecuteJob(runContext, job, scope);
    }

    private IJob ResolveJob(IServiceProvider scopedServiceProvider, JobDefinition definition)
    {
        if (definition.Type == typeof(DynamicJobFactory))
            return dynamicJobFactoryRegistry.GetJobInstance(scopedServiceProvider, definition);

        var job = scopedServiceProvider.GetService(definition.Type);
        if (job != null)
            return (IJob)job;

        LogUnregisteredJob(definition.Type);
        job = ActivatorUtilities.CreateInstance(scopedServiceProvider, definition.Type);

        return (IJob)job;
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        shutdown.Dispose();
        isDisposed = true;
    }

    private async Task ExecuteJob(JobExecutionContext runContext, IJob job, AsyncServiceScope serviceScope)
    {
        var stoppingToken = runContext.JobRun.CancellationToken;

        try
        {
            runContext.JobRun.NotifyStateChange(JobStateType.Running);
            LogRunningJob(job.GetType(), runContext.CorrelationId);

            await retryHandler.ExecuteAsync(async token => await job.RunAsync(runContext, token), runContext, stoppingToken);

            stoppingToken.ThrowIfCancellationRequested();

            runContext.JobRun.NotifyStateChange(JobStateType.Completing);

            await AfterJobCompletionTask(null, stoppingToken);
        }
        catch (Exception exc) when (exc is not OperationCanceledException or AggregateException)
        {
            runContext.JobRun.NotifyStateChange(JobStateType.Faulted, exc.Message);
            await NotifyExceptionHandlers(runContext, exc, stoppingToken);
            await AfterJobCompletionTask(exc, default);
        }
        // This needs to be async otherwise it can deadlock or try to use the disposed scope, maybe it needs to create its own serviceScope
        async Task AfterJobCompletionTask(Exception? exc, CancellationToken ct)
        {
            if (isDisposed)
            {
                LogSkipAsDisposed();
                return;
            }

            var notificationServiceType = typeof(IJobNotificationHandler<>).MakeGenericType(runContext.JobType);

            if (serviceScope.ServiceProvider.GetService(notificationServiceType) is IJobNotificationHandler notificationService)
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

            InformDependentJobs(runContext, exc is null);
        }
    }

    public void InformDependentJobs(JobExecutionContext context, bool success)
    {
        if (!context.ExecuteChildren)
        {
            return;
        }

        var jobRun = context.JobRun;
        var dependencies = success
            ? dependingJobRegistry.GetSuccessTypes(jobRun.JobDefinition.Type)
            : dependingJobRegistry.GetFaultedTypes(jobRun.JobDefinition.Type);

        if (dependencies.Count > 0)
            jobRun.NotifyStateChange(JobStateType.WaitingForDependency);

        foreach (var dependentJob in dependencies)
        {
            var newRun = JobRun.Create(dependentJob, dependentJob.Parameter, jobRun.CancellationToken);
            newRun.CorrelationId = jobRun.CorrelationId;
            newRun.ParentOutput = context.Output;
            newRun.IsOneTimeJob = true;
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
}
