using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NCronJob;

/// <summary>
/// Creates the <see cref="IJob"/> instance that executes a <see cref="JobDefinition"/>.
/// </summary>
internal sealed partial class JobResolver
{
    private readonly ILogger<JobResolver> logger;

    public JobResolver(ILogger<JobResolver> logger)
    {
        this.logger = logger;
    }

    public IJob Resolve(IServiceProvider scopedServiceProvider, JobDefinition definition)
    {
        if (!definition.IsTypedJob)
        {
            return new DynamicJobFactory(scopedServiceProvider, definition.JobDelegate);
        }

        if (scopedServiceProvider.GetService(definition.Type) is IJob job)
        {
            return job;
        }

        LogUnregisteredJob(definition.Name);

        return (IJob)ActivatorUtilities.CreateInstance(scopedServiceProvider, definition.Type);
    }

    [LoggerMessage(LogLevel.Warning, "The job '{JobName}' was not registered so an instance was created. Please register '{JobName}' for improved performance.")]
    private partial void LogUnregisteredJob(string jobName);
}
