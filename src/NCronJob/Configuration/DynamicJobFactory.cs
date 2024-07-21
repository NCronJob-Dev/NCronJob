using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace NCronJob;

internal class DynamicJobFactory : IJob
{
    private readonly IServiceProvider serviceProvider;
    private readonly Func<object[], Task> invoker;
    private readonly Func<IServiceProvider, object>?[] serviceResolvers;
    private readonly ParameterInfo[] parameters;

    public DynamicJobFactory(IServiceProvider serviceProvider, Delegate jobAction)
    {
        ArgumentNullException.ThrowIfNull(jobAction);

        this.serviceProvider = serviceProvider;
        parameters = jobAction.Method.GetParameters();
        serviceResolvers = BuildServiceResolvers();

        invoker = BuildInvoker(jobAction);

        Func<IServiceProvider, object>?[] BuildServiceResolvers() =>
            parameters.Select(p =>
                p.ParameterType == typeof(IJobExecutionContext) || p.ParameterType == typeof(CancellationToken)
                    ? null
                    : new Func<IServiceProvider, object>(sp => sp.GetRequiredService(p.ParameterType))
            ).ToArray();
    }

    private static Func<object[], Task> BuildInvoker(Delegate jobDelegate)
    {
        var method = jobDelegate.Method;
        var returnType = method.ReturnType;
        var param = Expression.Parameter(typeof(object[]), "args");
        var args = method.GetParameters().Select((p, index) =>
            Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(index)), p.ParameterType)).ToArray();
        var instance = method.IsStatic ? null : Expression.Constant(jobDelegate.Target);
        var call = Expression.Call(instance, method, args);

        if (returnType == typeof(Task))
        {
            var lambda = Expression.Lambda<Func<object[], Task>>(call, param);
            return lambda.Compile();
        }

        if (returnType == typeof(void))
        {
            var lambda = Expression.Lambda<Action<object[]>>(Expression.Block(call, Expression.Default(typeof(void))), param);
            var action = lambda.Compile();
            return objects => { action(objects); return Task.CompletedTask; };
        }

        throw new InvalidOperationException("The job action must return a Task or void type.");
    }

    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        var arguments = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(JobExecutionContext))
                arguments[i] = context;
            else if (parameters[i].ParameterType == typeof(CancellationToken))
                arguments[i] = token;
            else if (serviceResolvers[i] != null)
                arguments[i] = serviceResolvers[i]!(serviceProvider);
        }

        return invoker(arguments);
    }
}
