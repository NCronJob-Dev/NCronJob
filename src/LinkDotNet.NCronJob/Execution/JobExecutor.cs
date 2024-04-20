using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

internal sealed partial class JobExecutor : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<JobExecutor> logger;
    private readonly RetryHandler retryHandler;
    private bool isDisposed;
    private CancellationTokenSource? shutdown;

    public JobExecutor(IServiceProvider serviceProvider,
        ILogger<JobExecutor> logger,
        IHostApplicationLifetime lifetime,
        RetryHandler retryHandler)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.retryHandler = retryHandler;

        lifetime.ApplicationStopping.Register(() => this.shutdown?.Cancel());
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Service will be disposed in continuation task")]
    public async Task RunJob(RegistryEntry run, CancellationToken stoppingToken)
    {
        // stoppingToken is never cancelled when the job is triggered outside the BackgroundProcess,
        // so we need to tie into the IHostApplicationLifetime
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var stopToken = shutdown.Token;

        if (isDisposed)
        {
            LogSkipAsDisposed();
            return;
        }

        var scope = serviceProvider.CreateScope();
        if (scope.ServiceProvider.GetService(run.Type) is not IJob job)
        {
            LogJobNotRegistered(run.Type);
            return;
        }

        var jobExecutionInstance = new JobExecutionContext(run.Type, run.Output);
        await ExecuteJob(jobExecutionInstance, job, scope, stopToken);
    }

    public void Dispose()
    {
        shutdown?.Dispose();
        isDisposed = true;
    }

    private async Task ExecuteJob(JobExecutionContext runContext, IJob job, IServiceScope serviceScope, CancellationToken stoppingToken)
    {
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

            serviceScope.Dispose();
        }
    }
}
