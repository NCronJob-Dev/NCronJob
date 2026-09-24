using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Represents a stage in the job lifecycle where notifications are handled for the job.
/// </summary>
/// <typeparam name="TJob">The type of the job for which notifications are handled.</typeparam>
internal sealed class NotificationStage<TJob> : JobStage<TJob> where TJob : class, IJob
{
    internal NotificationStage(
        IServiceCollection services,
        IReadOnlyCollection<JobDefinition> jobDefinitions,
        ConcurrencySettings settings,
        JobDefinitionCollector jobDefinitionCollector)
        : base(services, jobDefinitions, settings, jobDefinitionCollector)
    {
    }

    protected override INotificationStage<TJob> AsNotificationStage() => this;
}
