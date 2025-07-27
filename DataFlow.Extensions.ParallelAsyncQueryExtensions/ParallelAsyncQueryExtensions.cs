
using System.Collections.Concurrent;
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

