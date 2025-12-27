using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NCronJob;

internal sealed partial class JobProcessor
{
    private readonly JobExecutor jobExecutor;
    private readonly ILogger<JobProcessor> logger;
    private readonly IServiceProvider serviceProvider;

    public JobProcessor(
        JobExecutor jobExecutor,
        ILogger<JobProcessor> logger,
        IServiceProvider serviceProvider)
    {
        this.jobExecutor = jobExecutor;
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    public async Task ProcessJobAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        try
        {
            if (jobRun.IsExpired)
            {
                LogDequeuingExpiredJob(jobRun.JobDefinition.Name);
                jobRun.NotifyStateChange(JobStateType.Expired);
                return;
            }

            jobRun.NotifyStateChange(JobStateType.Initializing);

            // Evaluate conditions before job instantiation
            if (jobRun.JobDefinition.Condition is not null)
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var shouldExecute = await jobRun.JobDefinition.Condition(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                
                if (!shouldExecute)
                {
                    LogJobConditionFailed(jobRun.JobDefinition.Name);
                    jobRun.NotifyStateChange(JobStateType.Skipped);
                    await TriggerConditionHandlers(jobRun, cancellationToken).ConfigureAwait(false);
                    return;
                }
                
                LogJobConditionSatisfied(jobRun.JobDefinition.Name);
            }

            await jobExecutor.RunJob(jobRun, cancellationToken).ConfigureAwait(false);

            jobRun.NotifyStateChange(JobStateType.Completed);
        }
        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested || oce.CancellationToken.IsCancellationRequested)
        {
            jobRun.NotifyStateChange(JobStateType.Cancelled);
        }
        catch (Exception ex)
        {
            jobRun.NotifyStateChange(JobStateType.Faulted, ex);
        }
        finally
        {
            jobRun.IncrementJobExecutionCount();
        }
    }

    [LoggerMessage(LogLevel.Trace, "Dequeuing job '{JobName}' because it has exceeded the expiration period.")]
    private partial void LogDequeuingExpiredJob(string jobName);

    [LoggerMessage(LogLevel.Debug, "Job '{JobName}' condition was not satisfied. Skipping execution.")]
    private partial void LogJobConditionFailed(string jobName);

    [LoggerMessage(LogLevel.Trace, "Job '{JobName}' condition was satisfied. Proceeding with execution.")]
    private partial void LogJobConditionSatisfied(string jobName);

    private async Task TriggerConditionHandlers(JobRun jobRun, CancellationToken cancellationToken)
    {
        if (!jobRun.JobDefinition.IsTypedJob)
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var handlerType = typeof(IJobConditionHandler<>).MakeGenericType(jobRun.JobDefinition.Type);

        if (scope.ServiceProvider.GetService(handlerType) is IJobConditionHandler handler)
        {
            try
            {
                var context = new JobConditionContext(jobRun);
                await handler.HandleConditionNotMetAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Don't throw exceptions from condition handlers
                LogConditionHandlerFailed(jobRun.JobDefinition.Name, ex);
            }
        }
    }

    [LoggerMessage(LogLevel.Warning, "Condition handler for job '{JobName}' threw an exception.")]
    private partial void LogConditionHandlerFailed(string jobName, Exception exception);
}
