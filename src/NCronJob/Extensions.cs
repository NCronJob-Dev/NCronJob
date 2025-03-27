namespace NCronJob;

internal static class NotNullLinqExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class => source.Where(x => x is not null)!;
}

internal static class DictionaryExtensions
{
    public static void AddInto<TKey, TCollection, TValue>(this IDictionary<TKey, TCollection> dic, TKey key, TValue value)
        where TKey : notnull
        where TCollection : ICollection<TValue>, new()
    {
        if (!dic.TryGetValue(key, out var entries))
        {
            entries = [];
            dic.Add(key, entries);
        }

        entries.Add(value);
    }
}
