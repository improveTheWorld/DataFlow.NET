namespace DataFlow.Extensions;

public static class EnumerableCasesExtension
{
    //------------------------------------------ Cases
    public static IEnumerable<(int categoryIndex, T item)> Cases<C, T>(this IEnumerable<(C category, T item)> items, params C[] categories)
    {
        var Dict = new Dictionary<C, int>(categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
        return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count, x.item));
    }



    private static int getFilterIndex<T>(this Func<T, bool>[] filters, T item)
    {

        int CategoryIndex = 0;
        foreach (var predicate in filters)
        {
            if (predicate(item))
                return CategoryIndex;
            else
                CategoryIndex++;
        }

        return CategoryIndex;
    }

    public static IEnumerable<(int category, T item)> Cases<T>(this IEnumerable<T> items, params Func<T, bool>[] filters)
    => items.Select(item => (filters.getFilterIndex(item), item));




    //----------------------------------------------- SelectCase

    public static IEnumerable<(int category, T item, R? newItem)> SelectCase<T, R>(this IEnumerable<(int category, T item)> items, params Func<T, R>[] selectors)
    => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item) : default));

    public static IEnumerable<(int category, T, R? item)> SelectCase<T, R>(this IEnumerable<(int category, T item)> items, params Func<T, int, R>[] selectors)
    => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item, idx) : default));

    //-----------------with newItem

    public static IEnumerable<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this IEnumerable<(int category, T item, R newItem)> items, params Func<R, Y>[] selectors)
   => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem) : default));

    public static IEnumerable<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this IEnumerable<(int category, T item, R newItem)> items, params Func<R, int, Y>[] selectors)
    => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem, idx) : default));

    //------------------------------------------- ForEachCase

    public static IEnumerable<(int category, T item)> ForEachCase<T>(this IEnumerable<(int category, T item)> items, params Action[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

    public static IEnumerable<(int category, T item)> ForEachCase<T>(this IEnumerable<(int category, T item)> items, params Action<T>[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });

    public static IEnumerable<(int category, T item)> ForEachCase<T>(this IEnumerable<(int category, T item)> items, params Action<T, int>[] actions)

    => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.item, index); });


    //-----------------with newItem
    public static IEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IEnumerable<(int category, T item, R newItem)> items, params Action[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

    public static IEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IEnumerable<(int category, T item, R newItem)> items, params Action<R>[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.newItem); });

    public static IEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IEnumerable<(int category, T item, R newItem)> items, params Action<R, int>[] actions)
    => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.newItem, index); });



    //------------------------------------UnCase
    public static IEnumerable<T> UnCase<T>(this IEnumerable<(int category, T item)> items)
    => items.Select(x => x.item);

    public static IEnumerable<T> UnCase<T, Y>(this IEnumerable<(int category, T item, Y newItem)> items)
    => items.Select(x => x.item);

    //------------------------------------AllCases
    public static IEnumerable<R> AllCases<T, R>(this IEnumerable<(int category, T item, R newItem)> items, bool filter = true)
    => filter ? items.Select(x => x.newItem).Where(x => x is not null && !x.Equals(default)) : items.Select(x => x.newItem);

}


