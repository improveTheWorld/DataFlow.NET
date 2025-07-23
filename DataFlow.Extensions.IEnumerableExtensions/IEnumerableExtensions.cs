using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static DataFlow.Extensions.Spy_IEnumerableExtension;

namespace DataFlow.Extensions;


public static class IEnumerableExtensions
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


    public static T? Cumul<T>(this IEnumerable<T> sequence, Func<T?, T, T> cumulate)
    {
        T? cumul = sequence.IsNullOrEmpty() ? default : sequence.First();

        sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x)).Do();

        return cumul;
    }

    public static TResult? Cumul<T, TResult>(this IEnumerable<T> sequence, Func<TResult?, T, TResult> cumulate, TResult? initial)
    {
        TResult? cumul = initial;

        sequence.ForEach(x => cumul = cumulate(cumul, x)).Do();

        return cumul;
    }

    public static StringBuilder BuildString(this IEnumerable<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}")
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


public static class IEnumerable_DeepLoopExtensions
{
    public static  IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items)
    {
        foreach (IEnumerable<T> seq in items)
        {
            foreach (var item in seq) yield return item;
        }
    }

    public static  IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items, T separator)
    {
        foreach (IEnumerable<T> seq in items)
        {
            foreach (var item in seq) yield return item;
            yield return separator;
        }
    }
}

public static class IEnumerable_CasesExtension
{
    //------------------------------------------ Cases
    public static IEnumerable<(int categoryIndex, T item)> Cases<C, T>(this IEnumerable<(C category, T item)> items, params C[] categories)
    {
        var Dict = new Dictionary<C, int>(categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
        return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count, x.item));
    }



    static int getFilterIndex<T>(this Func<T, bool>[] filters, T item)
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



    //------------------------------------AllCases
    public static IEnumerable<T> UnCase<T>(this IEnumerable<(int category, T item)> items)
    => items.Select(x => x.item);

    //------------------------------------AllCases
    public static IEnumerable<T> UnCase<T, Y>(this IEnumerable<(int category, T item, Y newItem)> items)
    => items.Select(x => x.item);

    public static IEnumerable<R> AllCases<T, R>(this IEnumerable<(int category, T item, R newItem)> items, bool filter = true)
    => filter ? items.Select(x => x.newItem).Where(x => x is not null && !x.Equals(default)) : items.Select(x => x.newItem);

    public static IEnumerable<string> ToLines(this IEnumerable<string> slices, string separator)
    {
        string sum = "";
        foreach (var slice in slices)
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
    public static IEnumerable<string> AllCases(this IEnumerable<(int category, string item)> items, string separator)
    {
        string sum = "";
        foreach (var (_, item) in items)
        {
            if (item != separator)
                sum += item;
            else
            {
                yield return sum;
                sum = "";
            }

        }
    }

}


//---------------------------------------------------IEnumerable<IEnumerable<T>>

public static class EnumeratorExtensions
{
    /// <summary>
    /// Advances the enumerator to the next element in the sequence,
    /// providing the result in an out parameter.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="enumerator">The enumerator to advance.</param>
    /// <param name="value">When this method returns, contains the element at the new
    /// position, or default(T) if the end of the sequence was reached.</param>
    /// <returns>
    /// true if the enumerator was successfully advanced to the next element;
    /// false if the enumerator has passed the end of the sequence.
    /// </returns>
    public static bool TryGetNext<T>(this IEnumerator<T> enumerator, out T? value)
    {
        if (enumerator.MoveNext())
        {
            value = enumerator.Current;
            return true;
        }

        value = default(T);
        return false;
    }
    public static T? GetNext<T>(this IEnumerator<T> enumerator)
    {
        if (enumerator.MoveNext())
        {
            return enumerator.Current;

        }
        else

            return default(T);
    }
}

public static class Enumerable_dataSourceExtensions
{
    /// <summary>
    /// Wraps a synchronous IEnumerable<T> in a cooperative IAsyncEnumerable<T>.
    /// It processes items in batches, yielding control periodically to prevent
    /// blocking the thread for extended periods.
    /// </summary>
    /// <param name="source">The source synchronous enumerable.</param>
    /// <param name="yieldThresholdMs">
    /// The time slice in milliseconds. After this much time has elapsed in a 
    /// synchronous batch, the method will yield control. Defaults to 15ms, which
    /// is ideal for maintaining UI responsiveness (under a 60fps frame budget).
    /// Set to long.MaxValue to effectively disable yielding and maximize throughput.
    /// </param>
    public static async IAsyncEnumerable<T> Async<T>(
        this IEnumerable<T> source,
        long yieldThresholdMs = 15) // Parameter with default value
    {
        // A threshold of 0 or less would yield on every item, which is inefficient.
        // We can treat it as a request to be highly responsive.
        if (yieldThresholdMs <= 0) yieldThresholdMs = 1;

        var stopwatch = Stopwatch.StartNew();

        foreach (var item in source)
        {
            yield return item;

            if (stopwatch.ElapsedMilliseconds > yieldThresholdMs)
            {
                await Task.Yield();
                stopwatch.Restart();
            }
        }
    }


    /// <summary>
    /// Throttles a synchronous sequence, converting it to an asynchronous one that emits items at a specified interval.
    /// </summary>
    /// <returns>An IAsyncEnumerable that yields items from the source sequence with a delay between each item.</returns>
    public static async IAsyncEnumerable<T> Throttle<T>(
        this IEnumerable<T> source,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public static async IAsyncEnumerable<T> Throttle<T>(
        this IEnumerable<T> source,
        double intervalInMS,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var interval = TimeSpan.FromMilliseconds(intervalInMS);
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}

public static class Spy_IEnumerableExtension
{

    public const string BEFORE = "---------{\n";
    public const string AFTER = "\n-------}";
    public const string SEPARATOR = "\n";
    public static IEnumerable<string> Spy(this IEnumerable<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    => items.Spy<string>(tag, x => x, timeStamp, separator, before, after);

    public static IEnumerable<T> Spy<T>(this IEnumerable<T> items, string tag, Func<T, string> customDispay, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    {
        string startedAt = string.Empty;
        Stopwatch stopwatch = new();
        if (timeStamp)
        {
            DateTime now = DateTime.Now;
            startedAt = $"[{now.Hour}:{now.Minute}:{now.Second}.{now.Millisecond}]";
            stopwatch = new Stopwatch();

            // Start the stopwatch
            stopwatch.Start();
        }

        Console.WriteLine(startedAt);
        if (!tag.IsNullOrEmpty())
            Console.Write(tag); Console.Write(" :");

        Console.Write(before);
        int i = 0;
        foreach (var item in items)
        {
            if (i != 0) Console.Write(separator);
            Console.Write(customDispay(item));
            yield return item;

            i++;
        }

        Console.Write(after);
        if (timeStamp)
        {
            // Stop the stopwatch
            stopwatch.Stop();
            Console.Write($"[{stopwatch.Elapsed.TotalMilliseconds} ms]");
        }

    }
}

public static class consoleMapper
{
    public static void Display(this IEnumerable<string> items, string tag = "Displaying", string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    {
        Console.WriteLine();
        if (!tag.IsNullOrEmpty())
            Console.Write(tag); Console.Write(" :");

        Console.Write(before);
        int i = 0;
        foreach (var item in items)
        {
            if (i != 0) Console.Write(separator);
            Console.Write(item);
            i++;
        }
        Console.Write(after);
    }
}

static public class DictionnaryExtensions
{
    public static bool AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
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
        if (dict.ContainsKey(key))
        {
            return dict[key];
        }
        else
        {
            return default;
        }
    }
}


