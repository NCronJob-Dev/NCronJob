using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Represents a stage in the job lifecycle where the job is set to run at startup.
/// </summary>
/// <typeparam name="TJob">The type of the job to be run at startup.</typeparam>
internal sealed class StartupStage<TJob> : JobStage<TJob>, IStartupStage<TJob> where TJob : class, IJob
{
    internal StartupStage(
        IServiceCollection services,
        IReadOnlyCollection<JobDefinition> jobDefinitions,
        ConcurrencySettings settings,
        JobDefinitionCollector jobDefinitionCollector)
        : base(services, jobDefinitions, settings, jobDefinitionCollector)
    {
    }

    /// <inheritdoc />
    public INotificationStage<TJob> RunAtStartup(bool shouldCrashOnFailure = false)
    {
        foreach (var jobDefinition in JobDefinitions)
        {
            jobDefinition.MarkAsStartupJob(shouldCrashOnFailure);
        }

        return AsNotificationStage();
    }

    protected override INotificationStage<TJob> AsNotificationStage() =>
        new NotificationStage<TJob>(Services, JobDefinitions, Settings, JobDefinitionCollector);
}
