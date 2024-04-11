using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob;

internal sealed partial class JobExecutor : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<JobExecutor> logger;
    private bool isDisposed;

    public JobExecutor(IServiceProvider serviceProvider, ILogger<JobExecutor> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Service will be disposed in continuation task")]
    public void RunJob(RegistryEntry run, CancellationToken stoppingToken)
    {
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

        ExecuteJob(run, job, scope, stoppingToken);
    }

    public void Dispose()
    {
        isDisposed = true;
    }

    private void ExecuteJob(RegistryEntry run, IJob job, IServiceScope serviceScope, CancellationToken stoppingToken)
    {
        try
        {
            LogRunningJob(run.Type);

            // We don't want to await jobs explicitly because that
            // could interfere with other job runs
            job.RunAsync(run.Context, stoppingToken)
                .ContinueWith(
                    task => AfterJobCompletionTask(task.Exception),
                    TaskScheduler.Default)
                .ConfigureAwait(false);
        }
        catch (Exception exc) when (exc is not OperationCanceledException or AggregateException)
        {
            // This part is only reached if the synchronous part of the job throws an exception
            AfterJobCompletionTask(exc);
        }

        void AfterJobCompletionTask(Exception? exc)
        {
            if (isDisposed)
            {
                LogSkipAsDisposed();
                return;
            }

            var notificationServiceType = typeof(IJobNotificationHandler<>).MakeGenericType(run.Type);

            if (serviceScope.ServiceProvider.GetService(notificationServiceType) is IJobNotificationHandler notificationService)
            {
                try
                {
                    notificationService.HandleAsync(run.Context, exc, stoppingToken).ConfigureAwait(false);
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
