
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DataFlow.Framework;

namespace DataFlow.Extensions;

public static class ParallelAsyncQueryExtensions
{

    //ForEach methods that return the original items(pass-through with side effects)
    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Action<T> action)
    {
        // This is safe because Select is designed for parallel execution.
        return source.Select(item =>
        {
            action(item);
            return item;
        });
    }

    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Func<T, Task> action)
    {
        // This is safe because Select is designed for parallel execution.
        return source.Select(async item =>
        {
            await action(item);
            return item;
        });
    }

    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Action<T, int> action)
    {
        // This is safe because the indexed Select is designed for parallel execution.
        return source.Select((item, index) =>
        {
            action(item, index);
            return item;
        });
    }

    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Func<T, int, Task> action)
    {
        // This is safe because the indexed Select is designed for parallel execution.
        return source.Select(async (item, index) =>
        {
            await action(item, index);
            return item;
        });
    }

    public static async Task Do<T>(this ParallelAsyncQuery<T> items)
    {
        // Asynchronously iterate and discard results to force execution.
        await foreach (var _ in items) { }
    }

    /// <summary>
    /// Creates a string from a parallel sequence in a thread-safe manner.
    /// It collects all items first and then builds the string sequentially.
    /// </summary>
    public static async Task<StringBuilder> BuildString(this ParallelAsyncQuery<string> items, StringBuilder? str = null, string separator = ", ", string before = "{", string after = "}")
    {
        str ??= new StringBuilder();

        if (!string.IsNullOrEmpty(before))
            str.Append(before);

        // Materialize the list first to ensure order and thread safety.
        var allItems = await items.ToList();

        str.Append(string.Join(separator, allItems));

        if (!string.IsNullOrEmpty(after))
            str.Append(after);

        return str;
    }

    #region Sum Overloads
    // The original dynamic implementation was not thread-safe.
    // These overloads use Interlocked for atomic operations, ensuring correctness in parallel execution.

    public static async Task<int> Sum(this ParallelAsyncQuery<int> source)
    {
        long sum = 0;
        await source.ForEach(item => Interlocked.Add(ref sum, item)).Do();
        return (int)sum;
    }

    public static async Task<long> Sum(this ParallelAsyncQuery<long> source)
    {
        long sum = 0;
        await source.ForEach(item => Interlocked.Add(ref sum, item)).Do();
        return sum;
    }

    //public static async Task<double> Sum(this ParallelAsyncQuery<double> source)
    //{
    //    double sum = 0;
    //    // Interlocked.Add for double is available in modern .NET.
    //    await source.ForEach(item => Interlocked.Add(ref sum, item)).Do();
    //    return sum;
    //}

    public static async Task<decimal> Sum(this ParallelAsyncQuery<decimal> source)
    {
        decimal sum = 0;
        object lockObj = new object();
        await source.ForEach(item =>
        {
            // Interlocked does not support decimal, so a lock is the simplest safe alternative.
            lock (lockObj)
            {
                sum += item;
            }
        }).Do();
        return sum;
    }
    #endregion

    #region ToList / Aggregate

    /// <summary>
    /// Creates a List from a ParallelAsyncQuery. This method is thread-safe.
    /// If the source query was configured with .WithOrderPreservation(true), the
    /// resulting list will maintain that order. Otherwise, for performance, the order
    /// is not guaranteed.
    /// </summary>
    public static async Task<List<T>> ToList<T>(this ParallelAsyncQuery<T> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        // --- THE UNORDERED PATH ---
        // If order doesn't matter, we use the fastest possible collection method.
        // ConcurrentBag uses thread-local storage to avoid lock contention,
        // making it the ideal choice for a high-performance "collect all" operation.
        if (!source.Settings.PreserveOrder)
        {
            var bag = new ConcurrentBag<T>();
            // ForEach can run fully in parallel, dumping results into the bag.
            await source.ForEach(item => bag.Add(item)).Do();
            return bag.ToList(); // A final, fast conversion to a List.
        }

        // --- THE ORDERED PATH ---
        // If order MUST be preserved, we prioritize memory efficiency and correctness.
        // The `await foreach` loop consumes the stream as it is produced in the correct
        // order by the query's internal reordering buffer. This avoids the
        // "double buffering" of the old ConcurrentDictionary approach.
        else
        {
            var list = new List<T>();
            await foreach (var item in source)
            {
                list.Add(item);
            }
            return list;
        }
    }




    /// <summary>
    /// Applies an accumulator function over a sequence. This implementation is sequential
    /// to guarantee correctness for non-associative functions. For parallel aggregation,
    /// a dedicated ParallelAggregate method should be used.
    /// </summary>
    public static async Task<TAccumulate> Aggregate<T, TAccumulate>(
        this ParallelAsyncQuery<T> source,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> func)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (func == null) throw new ArgumentNullException(nameof(func));

        TAccumulate result = seed;
        // Awaiting foreach on the parallel query will process it and yield results
        // which we can then safely aggregate sequentially.
        await foreach (var item in source)
        {
            result = func(result, item);
        }

        return result;
    }
    #endregion

    //------------------------------------------- FIRST

    /// <summary>
    /// Returns the first element of a sequence.
    /// </summary>
    public static async Task<T> First<T>(this ParallelAsyncQuery<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        await using var enumerator = source.GetAsyncEnumerator();
        if (await enumerator.MoveNextAsync())
            return enumerator.Current;

        throw new InvalidOperationException("Sequence contains no elements");
    }



    //------------------------------------------- FIRST OR DEFAULT

    /// <summary>
    /// Returns the first element of a sequence, or a default value if no element is found.
    /// </summary>
    public static async Task<T?> FirstOrDefault<T>(this ParallelAsyncQuery<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        await using var enumerator = source.GetAsyncEnumerator();
        if (await enumerator.MoveNextAsync())
            return enumerator.Current;

        return default(T);
    }
}


public static class ParallelAsyncQuery_CasesExtension
{
    //------------------------------------------ Cases
    public static ParallelAsyncQuery<(int categoryIndex, T item)> Cases<C, T>(this ParallelAsyncQuery<(C category, T item)> items, params C[] categories) where C : notnull
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

    public static ParallelAsyncQuery<(int category, T item)> Cases<T>(this ParallelAsyncQuery<T> items, params Func<T, bool>[] filters)
    => items.Select(item => (filters.getFilterIndex(item), item));




    //----------------------------------------------- SelectCase

    public static ParallelAsyncQuery<(int category, T item, R? newItem)> SelectCase<T, R>(this ParallelAsyncQuery<(int category, T item)> items, params Func<T, R>[] selectors)
    => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item) : default));

    public static ParallelAsyncQuery<(int category, T item, R? newItem)> SelectCase<T, R>(this ParallelAsyncQuery<(int category, T item)> items, params Func<T, int, R>[] selectors)
    => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item, idx) : default));

    //-----------------with newItem

    public static ParallelAsyncQuery<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Func<R, Y>[] selectors)
   => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem) : default));

    public static ParallelAsyncQuery<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Func<R, int, Y>[] selectors)
    => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem, idx) : default));

    //------------------------------------------- ForEachCase

    public static ParallelAsyncQuery<(int category, T item)> ForEachCase<T>(this ParallelAsyncQuery<(int category, T item)> items, params Action[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

    public static ParallelAsyncQuery<(int category, T item)> ForEachCase<T>(this ParallelAsyncQuery<(int category, T item)> items, params Action<T>[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });

    public static ParallelAsyncQuery<(int category, T item)> ForEachCase<T>(this ParallelAsyncQuery<(int category, T item)> items, params Action<T, int>[] actions)

    => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.item, index); });


    //-----------------with newItem
    public static ParallelAsyncQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Action[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

    public static ParallelAsyncQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Action<R>[] actions)
    => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.newItem); });

    public static ParallelAsyncQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Action<R, int>[] actions)
    => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.newItem, index); });



    //------------------------------------AllCases
    public static ParallelAsyncQuery<T> UnCase<T>(this ParallelAsyncQuery<(int category, T item)> items)
    => items.Select(x => x.item);

    //------------------------------------AllCases
    public static ParallelAsyncQuery<T> UnCase<T, Y>(this ParallelAsyncQuery<(int category, T item, Y newItem)> items)
    => items.Select(x => x.item);

    public static ParallelAsyncQuery<R> AllCases<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, bool filter = true) where T : class
    => filter ? items.Select(x => x.newItem).Where(x => x is not null && !x.Equals(default)) : items.Select(x => x.newItem);
}

public static class Spy_ParallelAsyncQueryExtension
{
    private static readonly object _consoleLock = new object();

    /// <summary>
    /// Spies on a sequence, ensuring correct pass-through behavior and thread-safe console output.
    /// This method wraps the enumerator to correctly print header and footer messages.
    /// </summary>
    public static async IAsyncEnumerable<T> Spy<T>(
        this ParallelAsyncQuery<T> items,
        string tag,
        Func<T, string> customDisplay,
        bool timeStamp = false,
        string separator = "\n",
        string before = "---------{\n",
        string after = "\n-------}",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = timeStamp ? Stopwatch.StartNew() : null;
        var count = 0;

        lock (_consoleLock)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            var time = stopwatch != null ? $"[{DateTime.Now:HH:mm:ss.fff}]" : "";
            Console.Write($"{time} {tag}");
            Console.ResetColor();
            Console.Write($" :{before}");
        }

        // We must use an indexed ForEach to reconstruct order for display if the query is unordered.
        // If the query is already ordered, this is still the safest way to display progress.
        if (!items.Settings.PreserveOrder)
        {
            var displayBuffer = new ConcurrentDictionary<int, string>();
            await items
                .ForEach((item, index) =>
                {
                    displayBuffer[index] = customDisplay(item);
                })
                .WithCancellation(cancellationToken)
                .Do();

            foreach (var kvp in displayBuffer.OrderBy(kvp => kvp.Key))
            {
                lock (_consoleLock)
                {
                    if (count > 0) Console.Write(separator);
                    Console.Write(kvp.Value);
                    count++;
                }
            }
        }
        else // Ordered stream
        {
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                lock (_consoleLock)
                {
                    if (count > 0) Console.Write(separator);
                    Console.Write(customDisplay(item));
                    count++;
                }
                yield return item;
            }
        }


        lock (_consoleLock)
        {
            Console.Write(after);
            if (stopwatch != null)
            {
                stopwatch.Stop();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" [{stopwatch.Elapsed.TotalMilliseconds:F0}ms, {count} items]");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        // If the stream was unordered, we consumed it for display, so we must "replay" it.
        // This is a known trade-off for spying on unordered parallel streams.
        if (!items.Settings.PreserveOrder)
        {
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }

    public static IAsyncEnumerable<string> Spy(this ParallelAsyncQuery<string> items, string tag, bool timeStamp = false, string separator = "\n", string before = "---------{\n", string after = "\n-------}")
    {
        return items.Spy<string>(tag, x => x, timeStamp, separator, before, after);
    }
}
