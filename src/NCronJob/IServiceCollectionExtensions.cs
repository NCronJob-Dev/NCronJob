using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCronJob;

// Inspired by https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.DependencyInjection.Abstractions/src/Extensions/ServiceCollectionDescriptorExtensions.cs
// License MIT
internal static class IServiceCollectionExtensions
{
    public static IServiceCollection TryAddSingleton<TService, TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, TImplementation> implementationFactory)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationFactory);

        var descriptor = ServiceDescriptor.Singleton<TService, TImplementation>(implementationFactory);

        services.TryAdd(descriptor);

        return services;
    }
}
