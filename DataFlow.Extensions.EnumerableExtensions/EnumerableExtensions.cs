using System.Text;

namespace DataFlow.Extensions;

public static class EnumerableExtensions
{
    /// <summary>
    /// Merge two ordered enumerables into a single ordered enumerable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerables.</typeparam>
    /// <param name="first">The first ordered enumerable.</param>
    /// <param name="second">The second ordered enumerable.</param>
    /// <param name="isFirstParamInferiorOrEqualToSecond">A function that determines if an element from the first enumerable is less than or equal to an element from the second enumerable.</param>
    /// <returns>An ordered enumerable that combines the elements from both input enumerables.</returns>
    /// <remarks>
    /// This method merges two ordered enumerables into a single ordered enumerable. It compares elements from each enumerable using the provided comparison function and yields the elements in the correct order.
    /// 
    /// The method handles the following cases:
    /// - If one of the enumerables is empty, the elements from the other enumerable are yielded.
    /// - If both enumerables are empty, an empty enumerable is returned.
    /// - If elements from both enumerables are available, they are compared using the provided function and the smaller element is yielded first.
    /// - Once one of the enumerables is exhausted, the remaining elements from the other enumerable are yielded.
    /// </remarks>
    public static IEnumerable<T> MergeOrdered<T>(this IEnumerable<T> first, IEnumerable<T> second, Func<T, T, bool> isFirstLessThanOrEqualToSecond)
    {
        using var enum1 = first?.GetEnumerator();
        using var enum2 = second?.GetEnumerator();

        bool hasNext1 = enum1?.MoveNext() ?? false;
        bool hasNext2 = enum2?.MoveNext() ?? false;

        while (hasNext1 && hasNext2)
        {
            if (isFirstLessThanOrEqualToSecond(enum1.Current, enum2.Current))
            {
                yield return enum1.Current;
                hasNext1 = enum1.MoveNext();
            }
            else
            {
                yield return enum2.Current;
                hasNext2 = enum2.MoveNext();
            }
        }

        var remainingEnumerator = hasNext1 ? enum1 : hasNext2 ? enum2 : null;

        while (remainingEnumerator?.MoveNext() ?? false)
        {
            yield return remainingEnumerator.Current;
        }
    }


    public static IEnumerable<T> Take<T>(this IEnumerable<T> sequence, int start, int count)
            => sequence.Take(new Range(start, start + count - 1));


    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<bool> stopCondition)
    {
        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

        foreach (var item in items)
        {
            yield return item;

            if (stopCondition())
            {
                break;
            }
        }
    }

    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, bool> stopCondition)
    {

        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

        foreach (var item in items)
        {
            yield return item;

            if (stopCondition(item))
            {
                break;
            }
        }
    }

    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, int, bool> stopCondition)
    {
        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

        int index = 0;

        foreach (var item in items)
        {
            yield return item;

            if (stopCondition(item, index++))
            {
                break;
            }
        }
    }

    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, int lastItemIdx)
    {
        int index = 0;

        foreach (var item in items)
        {
            yield return item;

            if (lastItemIdx == index++) break;
        }
    }

    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T, int> action)
    {
        return items.Select((x, idx) =>
        {
            action(x, idx);
            return x;
        });
    }
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        return items.Select(x =>
                    {
                        action(x);
                        return x;
                    });
    }


    public static void Do<T>(this IEnumerable<T> items, Action action)
    {
        foreach (var item in items)
        {
            action();
        }
    }

    public static void Do<T>(this IEnumerable<T> items)
    {
        foreach (var item in items) ;
    }

    public static StringBuilder BuildString(this IEnumerable<string> items, StringBuilder? str = null, string separator = ", ", string before = "{", string after = "}")
    {
        if (str is null) str = new StringBuilder();

        if (!before.IsNullOrEmpty())
            str.Append(before);

        items.ForEach((x, idx) => { if (idx > 0) str.Append(separator); str.Append(x); }).Do();

        if (!after.IsNullOrEmpty())
            str.Append(after);
        return str;
    }
    public static StringBuilder BuildString(this IEnumerable<string> items, string separator = ", ", string before = "{", string after = "}")
    {
        return items.BuildString(new StringBuilder(), separator, before, after);
    }


    public static bool IsNullOrEmpty<T>(this IEnumerable<T> sequence)
    {
        if (sequence == null)
        {
            return true;
        }

        var collection = sequence as ICollection<T>;
        if (collection != null)
        {
            return collection.Count == 0;
        }

        return !sequence.Any();
    }

}



