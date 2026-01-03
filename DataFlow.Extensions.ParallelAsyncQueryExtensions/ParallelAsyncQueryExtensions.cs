using System.Collections.Concurrent;
using System.Text;
using DataFlow.Framework;

namespace DataFlow.Parallel;

/// <summary>
/// Provides extension methods for <see cref="ParallelAsyncQuery{T}"/> that mirror common LINQ
/// / streaming patterns (ForEach, aggregation, materialization, etc.) while preserving the
/// query's parallel execution model.
/// </summary>
/// <remarks>
/// <para>
/// All methods in this class are pure extension helpers; the underlying <see cref="ParallelAsyncQuery{T}"/>
/// remains an immutable, lazily-evaluated pipeline builder. Methods that return
/// <see cref="ParallelAsyncQuery{T}"/> (e.g. the <c>ForEach</c> overloads) are <b>compositional</b>
/// and do not force execution; methods returning <see cref="Task"/>, scalar values, or materialized
/// collections (<c>ToList</c>, <c>First</c>, <c>Sum</c>, etc.) are <b>terminal operations</b> that
/// trigger enumeration of the source query.
/// </para>
/// <para>
/// Unless otherwise stated, these extensions honor the configuration defined in the originating
/// query's <see cref="ParallelExecutionSettings"/> (e.g. <c>PreserveOrder</c>, <c>MaxConcurrency</c>,
/// <c>ContinueOnError</c>, <c>OperationTimeout</c>). When <c>PreserveOrder</c> is <c>false</c> the
/// logical order of items is not guaranteed; callers relying on ordering should enable it explicitly.
/// </para>
/// <para>
/// Thread‑safety: All side effects invoked by <c>ForEach</c> overloads may run concurrently on
/// multiple threads. Callers must ensure that provided delegates and any captured state are thread-safe.
/// </para>
/// </remarks>
public static class ParallelAsyncQueryExtensions
{
    #region ForEach (Pass-Through Side-Effect) Overloads

    /// <summary>
    /// Registers a synchronous side-effect to be executed for each element during parallel processing,
    /// returning a new pass-through query that preserves the original elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <param name="action">A thread-safe action to invoke per element.</param>
    /// <returns>The original sequence wrapped with the additional side-effect stage.</returns>
    /// <remarks>
    /// <para>Lazy: no enumeration occurs until a terminal operation is invoked.</para>
    /// <para>
    /// Concurrency: <paramref name="action"/> may be invoked concurrently on multiple items.
    /// The order of invocation is not guaranteed unless <c>PreserveOrder</c> was configured.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Action<T> action)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (action == null) throw new ArgumentNullException(nameof(action));

        return source.Select(item =>
        {
            action(item);
            return item;
        });
    }

    /// <summary>
    /// Registers an asynchronous side-effect to be executed for each element during parallel processing,
    /// returning a new pass-through query that preserves the original elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <param name="action">An asynchronous, thread-safe function to invoke per element.</param>
    /// <returns>The original sequence wrapped with the additional asynchronous side-effect stage.</returns>
    /// <remarks>
    /// <para>Lazy: execution of <paramref name="action"/> is deferred until enumeration.</para>
    /// <para>
    /// Concurrency: Multiple <paramref name="action"/> invocations may be in-flight concurrently.
    /// Awaited tasks are incorporated into the stage ordering logic (respecting timeouts / cancellation).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Func<T, Task> action)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (action == null) throw new ArgumentNullException(nameof(action));

        return source.Select(async item =>
        {
            await action(item).ConfigureAwait(false);
            return item;
        });
    }

    /// <summary>
    /// Registers a synchronous side-effect with an element index to be executed for each element
    /// during parallel processing, returning a pass-through query.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <param name="action">A thread-safe action receiving element value and zero-based index.</param>
    /// <returns>A wrapped query stage that preserves original elements.</returns>
    /// <remarks>
    /// <para>
    /// Index semantics: The index supplied corresponds to the logical enumeration index. When
    /// <c>PreserveOrder = false</c> the timing of <paramref name="action"/> invocations may not match ascending index order.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Action<T, int> action)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (action == null) throw new ArgumentNullException(nameof(action));

        return source.Select((item, index) =>
        {
            action(item, index);
            return item;
        });
    }

    /// <summary>
    /// Registers an asynchronous side-effect with an element index to be executed for each element
    /// during parallel processing, returning a pass-through query.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <param name="action">A thread-safe asynchronous function receiving element value and zero-based index.</param>
    /// <returns>A wrapped query stage that preserves original elements.</returns>
    /// <remarks>
    /// <para>Lazy; enumeration triggers the asynchronous side-effects.</para>
    /// <para>Index ordering not guaranteed unless <c>PreserveOrder</c> is <c>true</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> source, Func<T, int, Task> action)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (action == null) throw new ArgumentNullException(nameof(action));

        return source.Select(async (item, index) =>
        {
            await action(item, index).ConfigureAwait(false);
            return item;
        });
    }

    #endregion

    #region Do (Terminal Execution)

    /// <summary>
    /// Forces asynchronous enumeration of a <see cref="ParallelAsyncQuery{T}"/> without
    /// producing a result, causing all upstream side-effects / transformations to execute.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The query to enumerate.</param>
    /// <returns>A task that completes when the sequence has been fully consumed.</returns>
    /// <remarks>
    /// <para>Equivalent to enumerating with <c>await foreach</c> and discarding all elements.</para>
    /// <para>Use to force materialization after configuring side-effecting stages (e.g. <c>ForEach</c>).</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
    public static async Task Do<T>(this ParallelAsyncQuery<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        await foreach (var _ in items.ConfigureAwait(false)) { /* discard */ }
    }

    /// <summary>
    /// Forces asynchronous enumeration of a <see cref="ParallelAsyncQuery{T}"/>, executing
    /// an action for each element.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The query to enumerate.</param>
    /// <param name="action">A thread-safe action to invoke per element.</param>
    /// <returns>A task that completes when the sequence has been fully consumed.</returns>
    /// <remarks>
    /// <para>Eager terminal operation. Equivalent to <c>await items.ForEach(action).Do()</c>.</para>
    /// <para>Combines side-effect execution with terminal consumption in a single call.</para>
    /// <para>Concurrency: action may be invoked concurrently on multiple items.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static async Task Do<T>(this ParallelAsyncQuery<T> items, Action<T> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));
        await items.ForEach(action).Do().ConfigureAwait(false);
    }

    /// <summary>
    /// Forces asynchronous enumeration of a <see cref="ParallelAsyncQuery{T}"/>, executing
    /// an indexed action for each element.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The query to enumerate.</param>
    /// <param name="action">A thread-safe action to invoke per element, receiving element and zero-based index.</param>
    /// <returns>A task that completes when the sequence has been fully consumed.</returns>
    /// <remarks>
    /// <para>Eager terminal operation. Equivalent to <c>await items.ForEach(action).Do()</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static async Task Do<T>(this ParallelAsyncQuery<T> items, Action<T, int> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));
        await items.ForEach(action).Do().ConfigureAwait(false);
    }

    #endregion

    #region BuildString

    /// <summary>
    /// Materializes a parallel string query to a <see cref="StringBuilder"/>, joining items
    /// with a separator and optional surrounding delimiters.
    /// </summary>
    /// <param name="items">The source parallel string query.</param>
    /// <param name="str">Optional existing <see cref="StringBuilder"/> to append to; a new one is created if <c>null</c>.</param>
    /// <param name="separator">The separator inserted between items (default: ", ").</param>
    /// <param name="before">A prefix string appended before the joined content (default: "{").</param>
    /// <param name="after">A suffix string appended after the joined content (default: "}").</param>
    /// <returns>
    /// A task producing the populated <see cref="StringBuilder"/> once the entire sequence is consumed.
    /// </returns>
    /// <remarks>
    /// <para>Terminal operation: forces full enumeration and loads all items into memory.</para>
    /// <para>
    /// Order Preservation: If <c>PreserveOrder = true</c> the original logical order is respected;
    /// otherwise resulting order is unspecified.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
    public static async Task<StringBuilder> BuildString(
        this ParallelAsyncQuery<string> items,
        StringBuilder? str = null,
        string separator = ", ",
        string before = "{",
        string after = "}")
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        str ??= new StringBuilder();

        if (!string.IsNullOrEmpty(before))
            str.Append(before);

        var allItems = await items.ToList().ConfigureAwait(false); // Materialize respecting order setting
        str.Append(string.Join(separator, allItems));

        if (!string.IsNullOrEmpty(after))
            str.Append(after);

        return str;
    }

    /// <summary>
    /// Materializes a parallel string query to a new <see cref="StringBuilder"/>, joining items
    /// with a separator and optional surrounding delimiters.
    /// </summary>
    /// <param name="items">The source parallel string query.</param>
    /// <param name="separator">The separator inserted between items (default: ", ").</param>
    /// <param name="before">A prefix string appended before the joined content (default: "{").</param>
    /// <param name="after">A suffix string appended after the joined content (default: "}").</param>
    /// <returns>
    /// A task producing a new <see cref="StringBuilder"/> with the concatenated text.
    /// </returns>
    /// <remarks>
    /// See <see cref="BuildString(ParallelAsyncQuery{string}, StringBuilder?, string, string, string)"/> for details.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
    public static Task<StringBuilder> BuildString(
        this ParallelAsyncQuery<string> items,
        string separator = ", ",
        string before = "{",
        string after = "}")
    {
        return items.BuildString(null, separator, before, after);
    }

    #endregion


    #region Sum Overloads

    /// <summary>
    /// Computes the sum of an <see cref="int"/> parallel query using atomic operations.
    /// </summary>
    /// <param name="source">The source of integer values.</param>
    /// <returns>A task producing the summed value.</returns>
    /// <remarks>
    /// <para>Associative and commutative operation; safe with unordered parallel aggregation.</para>
    /// <para>Overflow: Accumulates in <see cref="long"/> to reduce intermediate overflow risk, then casts to <see cref="int"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="OverflowException">Thrown if the resulting sum is outside the range of <see cref="int"/> (very large sequences).</exception>
    public static async Task<int> Sum(this ParallelAsyncQuery<int> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        long sum = 0;
        await source.ForEach(item => Interlocked.Add(ref sum, item)).Do().ConfigureAwait(false);
        if (sum > int.MaxValue || sum < int.MinValue)
            throw new OverflowException("The sum exceeds the range of Int32.");
        return (int)sum;
    }

    /// <summary>
    /// Computes the sum of a <see cref="long"/> parallel query using atomic operations.
    /// </summary>
    /// <param name="source">The source of 64-bit integer values.</param>
    /// <returns>A task producing the summed value.</returns>
    /// <remarks>Associative and commutative; ordering not required.</remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
    public static async Task<long> Sum(this ParallelAsyncQuery<long> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        long sum = 0;
        await source.ForEach(item => Interlocked.Add(ref sum, item)).Do().ConfigureAwait(false);
        return sum;
    }

    /// <summary>
    /// Computes the sum of a <see cref="decimal"/> parallel query using a lock for thread-safety.
    /// </summary>
    /// <param name="source">The source of decimal values.</param>
    /// <returns>A task producing the summed value.</returns>
    /// <remarks>
    /// <para><see cref="decimal"/> is not supported by <see cref="Interlocked"/>; a simple lock is used.</para>
    /// <para>For very hot paths with many small decimal additions you may consider batching or scaling strategies.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
    public static async Task<decimal> Sum(this ParallelAsyncQuery<decimal> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        decimal sum = 0;
        object lockObj = new object();
        await source.ForEach(item =>
        {
            lock (lockObj)
            {
                sum += item;
            }
        }).Do().ConfigureAwait(false);
        return sum;
    }

    #endregion

    #region ToList / Aggregate

    /// <summary>
    /// Materializes the parallel query into a <see cref="List{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <returns>A task producing a list of all elements.</returns>
    /// <remarks>
    /// <para>
    /// If <c>PreserveOrder = true</c> the list preserves logical order using the internal
    /// reordering buffer. Otherwise a fast unordered collection strategy is used (via <see cref="ConcurrentBag{T}"/>),
    /// and the resulting item order is unspecified.
    /// </para>
    /// <para>Terminal operation: enumerates the entire sequence.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
    public static async Task<List<T>> ToList<T>(this ParallelAsyncQuery<T> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        if (!source.Settings.PreserveOrder)
        {
            var bag = new ConcurrentBag<T>();
            await source.ForEach(item => bag.Add(item)).Do().ConfigureAwait(false);
            return bag.ToList();
        }
        else
        {
            var list = new List<T>();
            await foreach (var item in source.ConfigureAwait(false))
            {
                list.Add(item);
            }
            return list;
        }
    }

    /// <summary>
    /// Applies an accumulator function over the sequence in a sequential manner
    /// (on the consumer side) to guarantee correctness for non-associative operations.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TAccumulate">The accumulator/result type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="func">A function that combines the current accumulator and the next element.</param>
    /// <returns>A task producing the final accumulated value.</returns>
    /// <remarks>
    /// <para>
    /// This method enumerates the parallel query and applies <paramref name="func"/> on the client
    /// thread sequentially after each element is yielded. For purely associative &amp; commutative
    /// operations you may obtain higher performance by implementing a custom parallel reduction.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="source"/> or <paramref name="func"/> is <c>null</c>.
    /// </exception>
    public static async Task<TAccumulate> Aggregate<T, TAccumulate>(
        this ParallelAsyncQuery<T> source,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> func)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (func == null) throw new ArgumentNullException(nameof(func));

        var result = seed;
        await foreach (var item in source.ConfigureAwait(false))
        {
            result = func(result, item);
        }
        return result;
    }

    #endregion

    #region First / FirstOrDefault

    /// <summary>
    /// Returns the first element of the parallel query, throwing if the sequence is empty.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <returns>A task producing the first element.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the sequence contains no elements.</exception>
    public static async Task<T> First<T>(this ParallelAsyncQuery<T> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        await using var enumerator = source.GetAsyncEnumerator();
        if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            return enumerator.Current;

        throw new InvalidOperationException("Sequence contains no elements.");
    }

    /// <summary>
    /// Returns the first element of the parallel query, or the default value if the sequence is empty.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source parallel async query.</param>
    /// <returns>
    /// A task producing the first element, or <c>default(T)</c> if none exist.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
    public static async Task<T?> FirstOrDefault<T>(this ParallelAsyncQuery<T> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        await using var enumerator = source.GetAsyncEnumerator();
        if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            return enumerator.Current;

        return default;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Determines whether a parallel async query is <c>null</c> or contains no elements.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="query">The source query. May be <c>null</c>.</param>
    /// <returns><c>true</c> if <paramref name="query"/> is <c>null</c> or empty; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// If non-null, this method enumerates at most one element to determine emptiness.
    /// </remarks>
    public static async Task<bool> IsNullOrEmpty<T>(this ParallelAsyncQuery<T>? query)
    {
        if (query == null) return true;
        await using var enumerator = query.GetAsyncEnumerator();
        return !await enumerator.MoveNextAsync().ConfigureAwait(false);
    }

    #endregion
}

