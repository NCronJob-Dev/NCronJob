using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

internal sealed class RuntimeServiceCollection : List<ServiceDescriptor>, IServiceCollection
{
    public ImmutableArray<JobDefinition> GetJobDefinitions()
        => [..this.Select(s => s.ImplementationInstance).OfType<JobDefinition>()];
}
