namespace NCronJob;

internal static class JobParameter
{
    // The job type itself counts, so that a job registered by its interface, e.g. AddJob<IJob<Report>>(), keeps its contract.
    public static Type? TypeOf(Type jobType) =>
        SingleParameterType(ParameterTypesOf(jobType.GetInterfaces().Prepend(jobType), typeof(IJob<>)), $"Job '{jobType}'");

    public static Type? TypeOf(Delegate jobDelegate) =>
        SingleParameterType(
            ParameterTypesOf(jobDelegate.Method.GetParameters().Select(p => p.ParameterType), typeof(IJobExecutionContext<>)),
            $"Job delegate '{jobDelegate.Method.Name}'");

    public static bool IsExecutionContext(Type type) =>
        type == typeof(IJobExecutionContext) || IsClosedOver(type, typeof(IJobExecutionContext<>));

    private static IEnumerable<Type> ParameterTypesOf(IEnumerable<Type> types, Type openGenericType) =>
        types.Where(t => IsClosedOver(t, openGenericType)).Select(t => t.GetGenericArguments()[0]);

    private static bool IsClosedOver(Type type, Type openGenericType) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == openGenericType;

    private static Type? SingleParameterType(IEnumerable<Type> candidates, string owner)
    {
        var parameterTypes = candidates.Distinct().ToArray();

        return parameterTypes.Length switch
        {
            0 => null,
            1 => parameterTypes[0],
            _ => throw new InvalidOperationException(
                $"{owner} declares more than one parameter type ({string.Join(", ", parameterTypes.Select(t => t.Name))}), but a job accepts a single parameter type."),
        };
    }

    public static void EnsureAccepted(Type? parameterType, string jobName, object? parameter)
    {
        if (parameter is null || parameterType?.IsInstanceOfType(parameter) == true)
        {
            return;
        }

        throw new ArgumentException(
            parameterType is null
                ? $"Job '{jobName}' does not accept a parameter. Implement {nameof(IJob)}<TParameter> to pass one."
                : $"Job '{jobName}' expects a parameter of type '{parameterType}' but got '{parameter.GetType()}'.",
            nameof(parameter));
    }
}
