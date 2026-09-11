using System.Linq.Expressions;

namespace NCronJob;

internal static class DelegateInvoker
{
    public static Func<object[], TResult> Build<TResult>(Delegate target)
    {
        var (call, args) = BuildCall(target);
        return Expression.Lambda<Func<object[], TResult>>(call, args).Compile();
    }

    public static Action<object[]> BuildAction(Delegate target)
    {
        var (call, args) = BuildCall(target);
        return Expression.Lambda<Action<object[]>>(call, args).Compile();
    }

    private static (MethodCallExpression Call, ParameterExpression Args) BuildCall(Delegate target)
    {
        var method = target.Method;
        var args = Expression.Parameter(typeof(object[]), "args");
        var arguments = method.GetParameters().Select((p, index) =>
            Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(index)), p.ParameterType));
        var instance = method.IsStatic ? null : Expression.Constant(target.Target);

        return (Expression.Call(instance, method, arguments), args);
    }
}
