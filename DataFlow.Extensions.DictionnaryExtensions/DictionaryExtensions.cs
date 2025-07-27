namespace DataFlow.Extensions;

public static class DictionaryExtensions
{
    public static bool AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value) where TKey : notnull
    {
        if (dict.ContainsKey(key))
        {
            dict[key] = value;
            return false;
        }
        else
        {
            dict.Add(key, value);
            return true;
        }
    }

    public static TValue? GetOrNull<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TKey : notnull
    {
        return dict.TryGetValue(key, out var value) ? value : default;
    }

}
