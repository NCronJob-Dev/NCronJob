using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

internal static class TypedJobHandlerInvoker
{
    private static readonly ConcurrentDictionary<(Type OpenHandlerType, Type JobType), Type> ClosedHandlerTypes = new();

    public static Task InvokeNotificationHandlerAsync(
        IServiceProvider serviceProvider,
        JobDefinition jobDefinition,
        Func<IJobNotificationHandler, Task> invoke) =>
        InvokeAsync(serviceProvider, typeof(IJobNotificationHandler<>), jobDefinition, invoke);

    public static Task InvokeConditionHandlerAsync(
        IServiceProvider serviceProvider,
        JobDefinition jobDefinition,
        Func<IJobConditionHandler, Task> invoke) =>
        InvokeAsync(serviceProvider, typeof(IJobConditionHandler<>), jobDefinition, invoke);

    private static async Task InvokeAsync<THandler>(
        IServiceProvider serviceProvider,
        Type openHandlerType,
        JobDefinition jobDefinition,
        Func<THandler, Task> invoke)
        where THandler : class
    {
        if (!jobDefinition.IsTypedJob)
        {
            return;
        }

        var handlerType = ClosedHandlerTypes.GetOrAdd(
            (openHandlerType, jobDefinition.Type),
            static key => key.OpenHandlerType.MakeGenericType(key.JobType));

        if (serviceProvider.GetService<IServiceProviderIsService>() is { } registeredServices
            && !registeredServices.IsService(handlerType))
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();

        if (scope.ServiceProvider.GetService(handlerType) is THandler handler)
        {
            await invoke(handler).ConfigureAwait(false);
        }
    }
}
