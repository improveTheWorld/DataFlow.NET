﻿using System.Text;


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


    /// <summary>
    /// Aggregate with seed value
    /// </summary>
    public static async Task<TAccumulate> Aggregate<T, TAccumulate>(
        this IAsyncEnumerable<T> source,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> func)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        TAccumulate result = seed;
        await foreach (var item in source)
        {
            result = func(result, item);
        }

        return result;
    }


    /// <summary>
    /// Aggregate without seed 
    /// </summary>
    public static async Task<TAccumulate?> Aggregate<TAccumulate>(
        this IAsyncEnumerable<TAccumulate> source,
        Func<TAccumulate?, TAccumulate, TAccumulate> func)
    {
        TAccumulate? result = default;
        bool first = true;
        await foreach (var item in source)
        {
            if (first)
            {
                result = item;
                first = false;
            }
            else
            {
                result = func(result, item);
            }
        }
        return result;
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

/// <summary>
/// Contains LINQ-style operators for IAsyncEnumerable<T>.
/// Consider using the System.Linq.Async package for a complete, official implementation.
/// </summary>
public static class AsyncLinqOperators
{


    public static async IAsyncEnumerable<R> Select<T, R>(this IAsyncEnumerable<T> items, Func<T, R> Selector)
    {
        await foreach (var item in items)
        {
            yield return Selector(item); // Assuming T can be cast to R
        }
    }
    public static async IAsyncEnumerable<R> Select<T, R>(this IAsyncEnumerable<T> items, Func<T, int, R> Selector)
    {
        int idx = 0;
        await foreach (var item in items)
        {
            yield return Selector(item, idx);
            idx++;
        }
    }


    //------------------------------------------- WHERE

    /// <summary>
    /// Filters a sequence of values based on a predicate.
    /// </summary>
    public static async IAsyncEnumerable<T> Where<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (predicate(item))
                yield return item;
        }
    }

    /// <summary>
    /// Filters with async predicate
    /// </summary>
    public static async IAsyncEnumerable<T> Where<T>(
        this IAsyncEnumerable<T> source,
        Func<T, Task<bool>> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (await predicate(item))
                yield return item;
        }
    }

    /// <summary>
    /// Filters with index-based predicate
    /// </summary>
    public static async IAsyncEnumerable<T> Where<T>(
        this IAsyncEnumerable<T> source,
        Func<T, int, bool> predicate)

    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        int index = 0;
        await foreach (var item in source)
        {
            if (predicate(item, index))
                yield return item;
            index++;
        }
    }

    //------------------------------------------- TAKE 

    /// <summary>
    /// Returns a specified number of contiguous elements from the start of a sequence.
    /// Standard LINQ version
    /// </summary>
    public static async IAsyncEnumerable<T> Take<T>(
        this IAsyncEnumerable<T> source,
        int count)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (count <= 0)
            yield break;

        int taken = 0;
        await foreach (var item in source)
        {
            if (taken >= count)
                break;

            yield return item;
            taken++;
        }
    }

    public static IAsyncEnumerable<T> Take<T>(this IAsyncEnumerable<T> sequence, int start, int count)
           => sequence.Take(new Range(start, start + count - 1));

    public static async IAsyncEnumerable<T> Take<T>(
    this IAsyncEnumerable<T> sequence,
    Range range)
    {
        if (range.Start.IsFromEnd || range.End.IsFromEnd)
            throw new ArgumentException("Range with IsFromEnd not supported for async sequences");

        int currentIndex = 0;
        int endIndex = range.End.Value;
        int start = range.Start.Value;

        await foreach (var item in sequence)
        {
            if (currentIndex >= start && currentIndex < endIndex)
            {
                yield return item;
            }

            currentIndex++;

            if (currentIndex >= endIndex)
                break;
        }
    }

    /// <summary>
    /// Returns elements from a sequence as long as a specified condition is true.
    /// </summary>
    public static async IAsyncEnumerable<T> Take<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> whilePredeicateFunction)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (whilePredeicateFunction == null)
            throw new ArgumentNullException(nameof(whilePredeicateFunction));

        await foreach (var item in source)
        {
            if (!whilePredeicateFunction(item))
                break;

            yield return item;
        }
    }

    /// <summary>
    /// Take While with async predicate
    /// </summary>
    public static async IAsyncEnumerable<T> Take<T>(
        this IAsyncEnumerable<T> source,
        Func<T, Task<bool>> whilePredeicateFunction)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (whilePredeicateFunction == null)
            throw new ArgumentNullException(nameof(whilePredeicateFunction));

        await foreach (var item in source)
        {
            if (!await whilePredeicateFunction(item))
                break;

            yield return item;
        }
    }
    //------------------------------------------- SKIP

    /// <summary>
    /// Bypasses a specified number of elements in a sequence and then returns the remaining elements.
    /// </summary>
    public static async IAsyncEnumerable<T> Skip<T>(
        this IAsyncEnumerable<T> source,
        int count)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        int skipped = 0;
        await foreach (var item in source)
        {
            if (skipped >= count)
                yield return item;
            else
                skipped++;
        }
    }

    /// <summary>
    /// Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements.
    /// </summary>
    public static async IAsyncEnumerable<T> Skip<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> whileConditionpredicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (whileConditionpredicate == null)
            throw new ArgumentNullException(nameof(whileConditionpredicate));

        bool yielding = false;
        await foreach (var item in source)
        {
            if (!yielding && !whileConditionpredicate(item))
                yielding = true;

            if (yielding)
                yield return item;
        }
    }

    /// <summary>
    /// Async predicate version of SkipWhile
    /// </summary>
    public static async IAsyncEnumerable<T> Skip<T>(
        this IAsyncEnumerable<T> source,
        Func<T, Task<bool>> whileConditionpredicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (whileConditionpredicate == null)
            throw new ArgumentNullException(nameof(whileConditionpredicate));

        bool yielding = false;
        await foreach (var item in source)
        {
            if (!yielding && !await whileConditionpredicate(item))
                yielding = true;

            if (yielding)
                yield return item;
        }
    }

    /// <summary>
    /// Bypasses elements with index-based predicate
    /// </summary>
    public static async IAsyncEnumerable<T> Skip<T>(
        this IAsyncEnumerable<T> source,
        Func<T, int, bool> whileConditionpredicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (whileConditionpredicate == null)
            throw new ArgumentNullException(nameof(whileConditionpredicate));

        bool yielding = false;
        int index = 0;
        await foreach (var item in source)
        {
            if (!yielding && !whileConditionpredicate(item, index))
                yielding = true;

            if (yielding)
                yield return item;

            index++;
        }
    }


    /// <summary>
    /// Determines whether any element of a sequence satisfies a condition.
    /// </summary>
    public static async Task<bool> Any<T>(this IAsyncEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        await using var enumerator = source.GetAsyncEnumerator();
        return await enumerator.MoveNextAsync();
    }

    /// <summary>
    /// Determines whether any element of a sequence satisfies a condition.
    /// </summary>
    public static async Task<bool> Any<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (predicate(item))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Async predicate version of Any
    /// </summary>
    public static async Task<bool> Any<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (await predicate(item))
                return true;
        }
        return false;
    }

    //------------------------------------------- FIRST

    /// <summary>
    /// Returns the first element of a sequence.
    /// </summary>
    public static async Task<T> First<T>(this IAsyncEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        await using var enumerator = source.GetAsyncEnumerator();
        if (await enumerator.MoveNextAsync())
            return enumerator.Current;

        throw new InvalidOperationException("Sequence contains no elements");
    }

    /// <summary>
    /// Returns the first element in a sequence that satisfies a specified condition.
    /// </summary>
    public static async Task<T> First<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (predicate(item))
                return item;
        }

        throw new InvalidOperationException("No element satisfies the condition in predicate");
    }

    /// <summary>
    /// Async predicate version of First
    /// </summary>
    public static async Task<T> First<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (await predicate(item))
                return item;
        }

        throw new InvalidOperationException("No element satisfies the condition in predicate");
    }

    //------------------------------------------- FIRST OR DEFAULT

    /// <summary>
    /// Returns the first element of a sequence, or a default value if no element is found.
    /// </summary>
    public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        await using var enumerator = source.GetAsyncEnumerator();
        if (await enumerator.MoveNextAsync())
            return enumerator.Current;

        return default(T);
    }

    /// <summary>
    /// Returns the first element that satisfies a condition or a default value if no such element is found.
    /// </summary>
    public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (predicate(item))
                return item;
        }

        return default(T);
    }

    /// <summary>
    /// Async predicate version of FirstOrDefault
    /// </summary>
    public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in source)
        {
            if (await predicate(item))
                return item;
        }

        return default(T);
    }

    //------------------------------------------- TO COLLECTIONS

    /// <summary>
    /// Creates a List from an IAsyncEnumerable.
    /// </summary>
    public static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    /// <summary>
    /// Creates an array from an IAsyncEnumerable.
    /// </summary>
    public static async Task<T[]> ToArray<T>(this IAsyncEnumerable<T> source)
    {
        var list = await source.ToList();
        return list.ToArray();
    }

    /// <summary>
    /// Creates a Dictionary from an IAsyncEnumerable.
    /// </summary>
    public static async Task<Dictionary<TKey, TValue>> ToDictionary<T, TKey, TValue>(
        this IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        Func<T, TValue> valueSelector,
        IEqualityComparer<TKey>? comparer = null) where TKey : notnull
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (keySelector == null)
            throw new ArgumentNullException(nameof(keySelector));
        if (valueSelector == null)
            throw new ArgumentNullException(nameof(valueSelector));

        var dictionary = new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);
        await foreach (var item in source)
        {
            dictionary.Add(keySelector(item), valueSelector(item));
        }
        return dictionary;
    }

    public static async IAsyncEnumerable<string> ToLines(this IAsyncEnumerable<string> slices, string separator)
    {
        string sum = "";
        await foreach (var slice in slices)
        {
            if (slice != separator)
                sum += slice;
            else
            {
                yield return sum;
                sum = "";
            }

        }
    }

    //------------------------------------------- BUFFER/BATCH

    /// <summary>
    /// Buffers elements into batches of specified size
    /// </summary>
    public static async IAsyncEnumerable<T[]> Buffer<T>(
        this IAsyncEnumerable<T> source,
        int size)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        var buffer = new List<T>(size);

        await foreach (var item in source)
        {
            buffer.Add(item);

            if (buffer.Count == size)
            {
                yield return buffer.ToArray();
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            yield return buffer.ToArray();
        }
    }
}







