namespace NCronJob;

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
