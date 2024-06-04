using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// This classed is used as emulation of <see cref="IServiceCollection"/> to get job definitions at runtime.
/// </summary>
internal sealed class RuntimeServiceCollection : List<ServiceDescriptor>, IServiceCollection
{
    public ImmutableArray<JobDefinition> GetJobDefinitions()
        => [..this.Select(s => s.ImplementationInstance).OfType<JobDefinition>()];

    public ImmutableArray<DynamicJobRegistration> GetDynamicJobFactoryRegistries()
        => [..this.Select(s => s.ImplementationInstance).OfType<DynamicJobRegistration>()];
}
