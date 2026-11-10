using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Registers jobs after startup as one unit: if any step fails, everything done so far is rolled back.
/// </summary>
internal sealed class RuntimeJobRegistrar
{
    private readonly Lock registrationLock = new();

    private readonly IServiceCollection services;
    private readonly JobRegistry jobRegistry;
    private readonly CronRunScheduler cronRunScheduler;
    private readonly JobQueueManager jobQueueManager;
    private readonly ConcurrencySettings concurrencySettings;

    public RuntimeJobRegistrar(
        IServiceCollection services,
        JobRegistry jobRegistry,
        CronRunScheduler cronRunScheduler,
        JobQueueManager jobQueueManager,
        ConcurrencySettings concurrencySettings)
    {
        this.services = services;
        this.jobRegistry = jobRegistry;
        this.cronRunScheduler = cronRunScheduler;
        this.jobQueueManager = jobQueueManager;
        this.concurrencySettings = concurrencySettings;
    }

    public bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder, [NotNullWhen(false)] out Exception? exception)
    {
        lock (registrationLock)
        {
            var trackedServices = new TrackingServiceCollection(services);
            var previousSettings = concurrencySettings.Clone();
            JobRegistryRegistration? registration = null;
            List<JobRun> scheduledRuns = [];
            List<string> createdQueueNames = [];
            var activationGate = new JobRunActivationGate();

            try
            {
                var pendingJobDefinitions = new PendingJobDefinitions();
                var builder = new NCronJobOptionBuilder(trackedServices, concurrencySettings, pendingJobDefinitions);
                jobBuilder(builder);
                builder.ValidateConcurrencySettings(jobRegistry.GetAllRootJobs());

                registration = jobRegistry.FeedFrom(pendingJobDefinitions);

                foreach (var jobDefinition in pendingJobDefinitions.Entries.Keys)
                {
                    cronRunScheduler.ScheduleNextRun(
                        jobDefinition,
                        onRunCreated: scheduledRuns.Add,
                        onQueueCreated: createdQueueNames.Add,
                        activationGate: activationGate);
                }

                activationGate.Activate();
                exception = null;
                return true;
            }
            catch (Exception ex)
            {
                List<Exception> rollbackExceptions = [];
                activationGate.Reject();

                TryRollback(
                    () => jobQueueManager.RemoveRuns(scheduledRuns, createdQueueNames),
                    rollbackExceptions);

                if (registration is not null)
                {
                    TryRollback(() => jobRegistry.Rollback(registration), rollbackExceptions);
                }

                TryRollback(trackedServices.Rollback, rollbackExceptions);
                TryRollback(() => concurrencySettings.RestoreFrom(previousSettings), rollbackExceptions);

                exception = rollbackExceptions.Count == 0
                    ? ex
                    : new AggregateException(
                        "Runtime job registration failed and rollback encountered additional errors.",
                        new[] { ex }.Concat(rollbackExceptions));
                return false;
            }
        }
    }

    private static void TryRollback(Action rollback, List<Exception> exceptions)
    {
        try
        {
            rollback();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }
}
