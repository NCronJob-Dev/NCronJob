using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace LinkDotNet.NCronJob.Registry;

/// <summary>
/// 
/// </summary>
public static class EndpointRouterBuilderExtensions
{
    /// <summary>
    /// Maps a POST request to trigger a specified job of type <typeparamref name="T"/> in the NCronJob system.
    /// </summary>
    /// <typeparam name="T">The type of the job to be triggered, which must implement the <see cref="IJob"/> interface.</typeparam>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to which the route is added.</param>
    /// <param name="url">The URL pattern of the route. This specifies where the POST request should be mapped.</param>
    /// <param name="parameter">An optional parameter that can be passed to the job when it is triggered.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route.</returns>
    public static RouteHandlerBuilder MapPostToNCronJob<T>(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string url, string? parameter = null)
        where T : class, IJob =>
        endpoints.MapPost(url,
            (IInstantJobRegistry instantJobRegistry) => instantJobRegistry.RunInstantJob<T>(parameter));
}
