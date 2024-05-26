using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobExecutor : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<JobExecutor> logger;
    private readonly IRetryHandler retryHandler;
    private readonly JobHistory jobHistory;
    private volatile bool isDisposed;
    private readonly CancellationTokenSource shutdown = new();

    public JobExecutor(
        IServiceProvider serviceProvider,
        ILogger<JobExecutor> logger,
        IHostApplicationLifetime lifetime,
        IRetryHandler retryHandler,
        JobHistory jobHistory)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.retryHandler = retryHandler;
        this.jobHistory = jobHistory;

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
        var stopToken = linkedCts.Token;
        run.CancellationToken = stopToken;

        await using var scope = serviceProvider.CreateAsyncScope();

        var job = ResolveJob(scope.ServiceProvider, run.JobDefinition);

        jobHistory.Add(run);
        var runContext = new JobExecutionContext(run);
        await ExecuteJob(runContext, job, scope);
    }

    private static IJob ResolveJob(IServiceProvider scopedServiceProvider, JobDefinition definition) =>
        typeof(DynamicJobFactory).IsAssignableFrom(definition.Type)
            ? (IJob)scopedServiceProvider.GetRequiredKeyedService(definition.Type, definition.JobName)
            : (IJob)scopedServiceProvider.GetRequiredService(definition.Type);

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
            LogRunningJob(job.GetType());

            await retryHandler.ExecuteAsync(async token => await job.RunAsync(runContext, token), runContext, stoppingToken);

            stoppingToken.ThrowIfCancellationRequested();

            await AfterJobCompletionTask(null, stoppingToken);
        }
        catch (Exception exc) when (exc is not OperationCanceledException or AggregateException)
        {
            // This part is only reached if the synchronous part of the job throws an exception
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
        }
    }
}
