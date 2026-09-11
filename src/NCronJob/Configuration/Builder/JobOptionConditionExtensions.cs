namespace NCronJob;

internal static class JobOptionConditionExtensions
{
    public static void AddCondition(this JobOption jobOption, Func<bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        jobOption.AddCondition((_, _) => new ValueTask<bool>(predicate()));
    }

    public static void AddCondition(this JobOption jobOption, Delegate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        jobOption.AddCondition(ConditionInvokerBuilder.BuildConditionInvoker(predicate));
    }

    public static void AddCondition(this JobOption jobOption, Func<Task<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        jobOption.AddCondition(async (_, _) => await predicate().ConfigureAwait(false));
    }

    private static void AddCondition(this JobOption jobOption, Func<IServiceProvider, CancellationToken, ValueTask<bool>> condition)
    {
        jobOption.Conditions ??= [];
        jobOption.Conditions.Add(condition);
    }
}
