namespace NCronJob;

internal static class NotNullLinqExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class => source.Where(x => x is not null)!;
}

internal static class DictionaryExtensions
{
    public static List<TValue> GetOrCreateList<TKey, TValue>(
        this IDictionary<TKey, List<TValue>> dic,
        TKey key
    )
        where TKey : notnull
    {
        if (!dic.TryGetValue(key, out var entries))
        {
            entries = [];
            dic[key] = entries;
        }
        return entries;
    }
}
