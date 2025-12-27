using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace NCronJob;

internal static class ConditionInvokerBuilder
{
    public static Func<IServiceProvider, CancellationToken, ValueTask<bool>> BuildConditionInvoker(Delegate predicate)
    {
        var method = predicate.Method;
        var returnType = method.ReturnType;
        var parameters = method.GetParameters();
        var serviceResolvers = ServiceResolverHelper.BuildServiceResolvers(parameters);

        if (returnType == typeof(bool))
        {
            var syncInvoker = BuildSyncInvoker(predicate);
            return (sp, ct) =>
            {
                var arguments = ServiceResolverHelper.ResolveArguments(sp, parameters, serviceResolvers, null, ct);
                var result = syncInvoker(arguments);
                return new ValueTask<bool>(result);
            };
        }
        
        if (returnType == typeof(Task<bool>))
        {
            var asyncInvoker = BuildAsyncInvoker(predicate);
            return async (sp, ct) =>
            {
                var arguments = ServiceResolverHelper.ResolveArguments(sp, parameters, serviceResolvers, null, ct);
                return await asyncInvoker(arguments).ConfigureAwait(false);
            };
        }
        
        if (returnType == typeof(ValueTask<bool>))
        {
            var valueTaskInvoker = BuildValueTaskInvoker(predicate);
            return (sp, ct) =>
            {
                var arguments = ServiceResolverHelper.ResolveArguments(sp, parameters, serviceResolvers, null, ct);
                return valueTaskInvoker(arguments);
            };
        }

        throw new InvalidOperationException(
            $"The condition predicate must return bool, Task<bool>, or ValueTask<bool>. Found: {returnType.Name}");
    }

    private static Func<object[], bool> BuildSyncInvoker(Delegate predicate)
    {
        var method = predicate.Method;
        var param = Expression.Parameter(typeof(object[]), "args");
        var args = method.GetParameters().Select((p, index) =>
            Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(index)), p.ParameterType)).ToArray();
        var instance = method.IsStatic ? null : Expression.Constant(predicate.Target);
        var call = Expression.Call(instance, method, args);
        var lambda = Expression.Lambda<Func<object[], bool>>(call, param);
        return lambda.Compile();
    }

    private static Func<object[], Task<bool>> BuildAsyncInvoker(Delegate predicate)
    {
        var method = predicate.Method;
        var param = Expression.Parameter(typeof(object[]), "args");
        var args = method.GetParameters().Select((p, index) =>
            Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(index)), p.ParameterType)).ToArray();
        var instance = method.IsStatic ? null : Expression.Constant(predicate.Target);
        var call = Expression.Call(instance, method, args);
        var lambda = Expression.Lambda<Func<object[], Task<bool>>>(call, param);
        return lambda.Compile();
    }

    private static Func<object[], ValueTask<bool>> BuildValueTaskInvoker(Delegate predicate)
    {
        var method = predicate.Method;
        var param = Expression.Parameter(typeof(object[]), "args");
        var args = method.GetParameters().Select((p, index) =>
            Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(index)), p.ParameterType)).ToArray();
        var instance = method.IsStatic ? null : Expression.Constant(predicate.Target);
        var call = Expression.Call(instance, method, args);
        var lambda = Expression.Lambda<Func<object[], ValueTask<bool>>>(call, param);
        return lambda.Compile();
    }
}
