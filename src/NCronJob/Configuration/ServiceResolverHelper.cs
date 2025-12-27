using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NCronJob;

internal static class ServiceResolverHelper
{
    public static Func<IServiceProvider, object>?[] BuildServiceResolvers(ParameterInfo[] parameters)
    {
        return parameters.Select(p =>
            p.ParameterType == typeof(IJobExecutionContext) || p.ParameterType == typeof(CancellationToken)
                ? null
                : new Func<IServiceProvider, object>(sp => sp.GetRequiredService(p.ParameterType))
        ).ToArray();
    }

    public static object[] ResolveArguments(
        IServiceProvider serviceProvider,
        ParameterInfo[] parameters,
        Func<IServiceProvider, object>?[] serviceResolvers,
        IJobExecutionContext? context,
        CancellationToken cancellationToken)
    {
        var arguments = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(IJobExecutionContext))
                arguments[i] = context!;
            else if (parameters[i].ParameterType == typeof(CancellationToken))
                arguments[i] = cancellationToken;
            else if (serviceResolvers[i] != null)
                arguments[i] = serviceResolvers[i]!(serviceProvider);
        }
        return arguments;
    }
}
