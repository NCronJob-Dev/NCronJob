namespace NCronJob;

internal static class ConditionInvokerBuilder
{
    public static Func<IServiceProvider, CancellationToken, ValueTask<bool>> BuildConditionInvoker(Delegate predicate)
    {
        var method = predicate.Method;
        var returnType = method.ReturnType;
        var parameters = method.GetParameters();
        var serviceResolvers = DelegateParameterResolver.BuildServiceResolvers(parameters);

        if (returnType == typeof(bool))
        {
            var syncInvoker = DelegateInvoker.Build<bool>(predicate);
            return (sp, ct) => new ValueTask<bool>(syncInvoker(ResolveArguments(sp, ct)));
        }

        if (returnType == typeof(Task<bool>))
        {
            var asyncInvoker = DelegateInvoker.Build<Task<bool>>(predicate);
            return (sp, ct) => new ValueTask<bool>(asyncInvoker(ResolveArguments(sp, ct)));
        }

        if (returnType == typeof(ValueTask<bool>))
        {
            var valueTaskInvoker = DelegateInvoker.Build<ValueTask<bool>>(predicate);
            return (sp, ct) => valueTaskInvoker(ResolveArguments(sp, ct));
        }

        throw new InvalidOperationException(
            $"The condition predicate must return bool, Task<bool>, or ValueTask<bool>. Found: {returnType.Name}");

        object[] ResolveArguments(IServiceProvider sp, CancellationToken ct)
            => DelegateParameterResolver.ResolveArguments(sp, parameters, serviceResolvers, null, ct);
    }
}
