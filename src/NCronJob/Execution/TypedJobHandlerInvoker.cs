using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

internal static class TypedJobHandlerInvoker
{
    public static async Task InvokeAsync<THandler>(
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

        await using var scope = serviceProvider.CreateAsyncScope();
        var handlerType = openHandlerType.MakeGenericType(jobDefinition.Type);

        if (scope.ServiceProvider.GetService(handlerType) is THandler handler)
        {
            await invoke(handler).ConfigureAwait(false);
        }
    }
}
