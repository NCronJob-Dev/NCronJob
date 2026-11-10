using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NCronJob;

internal static class DelegateParameterResolver
{
    public static Func<IServiceProvider, object>?[] BuildServiceResolvers(ParameterInfo[] parameters)
    {
        return parameters.Select(p =>
            JobParameter.IsExecutionContext(p.ParameterType) || p.ParameterType == typeof(CancellationToken)
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
            if (JobParameter.IsExecutionContext(parameters[i].ParameterType))
                arguments[i] = context!;
            else if (parameters[i].ParameterType == typeof(CancellationToken))
                arguments[i] = cancellationToken;
            else if (serviceResolvers[i] is not null)
                arguments[i] = serviceResolvers[i]!(serviceProvider);
        }
        return arguments;
    }
}
