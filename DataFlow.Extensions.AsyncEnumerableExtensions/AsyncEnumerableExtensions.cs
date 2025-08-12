using System.Text;


namespace DataFlow.Extensions;

/// <summary>
/// Contains fundamental extension methods for IAsyncEnumerable<T>.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Merge two ordered enumerables into a single ordered enumerable.
    /// </summary>
    public static async IAsyncEnumerable<T> MergeOrdered<T>(
    this IAsyncEnumerable<T> first,
    IAsyncEnumerable<T> second,
    Func<T, T, bool> isFirstLessThanOrEqualToSecond)
    {
        await using var enum1 = first?.GetAsyncEnumerator();
        await using var enum2 = second?.GetAsyncEnumerator();

        bool hasNext1 = enum1 != null && await enum1.MoveNextAsync();
        bool hasNext2 = enum2 != null && await enum2.MoveNextAsync();

        while (hasNext1 && hasNext2)
        {
            if (isFirstLessThanOrEqualToSecond(enum1!.Current, enum2!.Current))
            {
                yield return enum1.Current;
                hasNext1 = await enum1.MoveNextAsync();
            }
            else
            {
                yield return enum2.Current;
                hasNext2 = await enum2.MoveNextAsync();
            }
        }

        var remainingEnumerator = hasNext1 ? enum1 : hasNext2 ? enum2 : null;

        while (remainingEnumerator != null && await remainingEnumerator.MoveNextAsync())
        {
            yield return remainingEnumerator.Current;
        }
    }


    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<bool> stopCondition)
    {
        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

        await foreach (var item in items)
        {
            yield return item;

            if (stopCondition())
            {
                break;
            }
        }
    }

    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<T, bool> stopCondition)
    {

        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

        await foreach (var item in items)
        {
            yield return item;

            if (stopCondition(item))
            {
                break;
            }
        }
    }

    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<T, int, bool> stopCondition)
    {
        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

        int index = 0;

        await foreach (var item in items)
        {
            yield return item;

            if (stopCondition(item, index++))
            {
                break;
            }
        }
    }

    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, int lastItemIdx)
    {
        int index = 0;

        await foreach (var item in items)
        {
            yield return item;

            if (lastItemIdx == index++) break;
        }
    }

    public static IAsyncEnumerable<T> ForEach<T>(this IAsyncEnumerable<T> items, Action<T, int> action)
    {
        return items.Select((x, idx) =>
        {
            action(x, idx);
            return x;
        });
    }
    public static IAsyncEnumerable<T> ForEach<T>(this IAsyncEnumerable<T> items, Action<T> action)
    {
        return items.Select(x =>
        {
            action(x);
            return x;
        });
    }

    public static async Task Do<T>(this IAsyncEnumerable<T> items)
    {
        await foreach (var item in items) ;
    }



    public static async Task<StringBuilder> BuildString(this IAsyncEnumerable<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}")
    {
        if (str is null) str = new StringBuilder();

        if (!before.IsNullOrEmpty())
            str.Append(before);

        await items.ForEach((x, idx) => { if (idx > 0) str.Append(separator); str.Append(x); }).Do();

        if (!after.IsNullOrEmpty())
            str.Append(after);
        return str;
    }
    public static async Task<StringBuilder> BuildString(this IAsyncEnumerable<string> items, string separator = ", ", string before = "{", string after = "}")
    {
        return await items.BuildString(new StringBuilder(), separator, before, after);
    }



    public static async Task<bool> IsNullOrEmpty<T>(this IAsyncEnumerable<T> sequence)
    {
        if (sequence == null) return true;
        return !await sequence.Any();
    }
}







