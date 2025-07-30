namespace DataFlow.Extensions;

public static class ParallelQueryCasesExtension
{
    public static ParallelQuery<(int categoryIndex, T item)> Cases<C, T>(this ParallelQuery<(C category, T item)> items, params C[] categories) where C : notnull
    {
        var dict = categories.Select((category, idx) => new { category, idx })
                            .ToDictionary(x => x.category, x => x.idx);

        return items.Select(x => (dict.TryGetValue(x.category, out var index) ? index : dict.Count, x.item));
    }

    public static ParallelQuery<(int category, T item)> Cases<T>(this ParallelQuery<T> items, params Func<T, bool>[] filters)
    {
        return items.Select(item =>
        {
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i](item))
                    return (i, item);
            }
            return (filters.Length, item);
        });
    }

    // SelectCase methods
    public static ParallelQuery<(int category, T item, R? newItem)> SelectCase<T, R>(this ParallelQuery<(int category, T item)> items, params Func<T, R>[] selectors)
        => items.Select(x => (x.category, x.item, x.category < selectors.Length ? selectors[x.category](x.item) : default(R)));

    public static ParallelQuery<(int category, T item, R? newItem)> SelectCase<T, R>(this ParallelQuery<(int category, T item)> items, params Func<T, int, R>[] selectors)
        => items.Select((x, idx) => (x.category, x.item, x.category < selectors.Length ? selectors[x.category](x.item, idx) : default(R)));

    //-----------------with newItem

    public static ParallelQuery<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this ParallelQuery<(int category, T item, R newItem)> items, params Func<R, Y>[] selectors)
   => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem) : default));

    public static ParallelQuery<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this ParallelQuery<(int category, T item, R newItem)> items, params Func<R, int, Y>[] selectors)
    => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem, idx) : default));

    // ForEachCase methods
    public static ParallelQuery<(int category, T item)> ForEachCase<T>(this ParallelQuery<(int category, T item)> items, params Action[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

    public static ParallelQuery<(int category, T item)> ForEachCase<T>(this ParallelQuery<(int category, T item)> items, params Action<T>[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });
    public static ParallelQuery<(int category, T item)> ForEachCase<T>(this ParallelQuery<(int category, T item)> items, params Action<T, int>[] actions)
      => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.item, index); });

    //-----------------with newItem
    public static ParallelQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelQuery<(int category, T item, R newItem)> items, params Action[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

    public static ParallelQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelQuery<(int category, T item, R newItem)> items, params Action<R>[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.newItem); });

    public static ParallelQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelQuery<(int category, T item, R newItem)> items, params Action<R, int>[] actions)
    => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.newItem, index); });


    // UnCase methods
    public static ParallelQuery<T> UnCase<T>(this ParallelQuery<(int category, T item)> items)
        => items.Select(x => x.item);

    public static ParallelQuery<T> UnCase<T, Y>(this ParallelQuery<(int category, T item, Y newItem)> items)
        => items.Select(x => x.item);

    //------------------------------------AllCases
    public static ParallelQuery<R> AllCases<T, R>(this ParallelQuery<(int category, T item, R newItem)> items, bool filter = true)
        => filter ? items.Select(x => x.newItem).Where(x => x != null && !x.Equals(default(R)))
                 : items.Select(x => x.newItem);


}
