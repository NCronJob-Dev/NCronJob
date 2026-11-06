namespace NCronJob;

internal static class JobConditionChain
{
    /// <summary>
    /// Combines conditions so that all of them must be satisfied. Evaluation stops at the first condition that is not.
    /// </summary>
    public static Func<IServiceProvider, CancellationToken, ValueTask<bool>> Append(
        Func<IServiceProvider, CancellationToken, ValueTask<bool>>? previousCondition,
        IReadOnlyCollection<Func<IServiceProvider, CancellationToken, ValueTask<bool>>> addedConditions)
    {
        var added = addedConditions.ToArray();

        return async (sp, ct) =>
        {
            if (previousCondition is not null && !await previousCondition(sp, ct).ConfigureAwait(false))
                return false;

            foreach (var condition in added)
            {
                if (!await condition(sp, ct).ConfigureAwait(false))
                    return false;
            }

            return true;
        };
    }
}
