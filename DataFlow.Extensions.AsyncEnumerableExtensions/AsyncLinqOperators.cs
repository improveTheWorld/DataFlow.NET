using System.Text;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides lightweight, LINQ-style operators for <see cref="IAsyncEnumerable{T}"/> to make the
    /// DataFlow.Extensions package more self-contained without requiring the
    /// System.Linq.Async package. These operators are intentionally minimal,
    /// focus on clarity, and mirror the semantics of the synchronous LINQ
    /// methods wherever practical.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All sequence-returning operators (e.g. <c>Select</c>, <c>Where</c>, <c>Skip</c>, <c>Take</c>, <c>SelectMany</c>)
    /// are lazy: no iteration of the underlying source occurs until the returned
    /// <see cref="IAsyncEnumerable{T}"/> is enumerated with <c>await foreach</c>,
    /// or materialized via a terminal method such as <c>First</c>, <c>ToList</c>, etc.
    /// </para>
    /// <para>
    /// All arguments are validated; a <see cref="ArgumentNullException"/> is thrown if
    /// <paramref name="source"/> or a required delegate is <c>null</c>.
    /// </para>
    /// <para>
    /// These implementations are intentionally straightforward and may not be as
    /// optimized as those from <c>System.Linq.Async</c>. For production scenarios
    /// requiring advanced operators or performance tuning, consider adopting that NuGet package.
    /// </para>
    /// </remarks>
    public static class AsyncLinqOperators
    {
        #region Aggregate
        /// <summary>
        /// Applies an accumulator function over an asynchronous sequence using the specified seed value.
        /// </summary>
        /// <typeparam name="T">The element type of the source sequence.</typeparam>
        /// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="seed">The initial accumulator value.</param>
        /// <param name="func">An accumulator function to apply to each element.</param>
        /// <returns>
        /// A task whose result is the final accumulator value after all elements have been processed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="func"/> is null.</exception>
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
        /// Applies an accumulator function over an asynchronous sequence without an explicit seed.
        /// The first element (if any) becomes the initial accumulator value.
        /// </summary>
        /// <typeparam name="TAccumulate">The element and accumulator type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="func">An accumulator function invoked for each subsequent element.</param>
        /// <returns>
        /// A task whose result is the accumulated value; if the sequence is empty, the default value of <typeparamref name="TAccumulate"/> is returned.
        /// </returns>
        /// <remarks>
        /// If the sequence is empty, no exception is thrown (unlike classical LINQ's <c>Aggregate</c> without a seed).
        /// </remarks>
        public static async Task<TAccumulate?> Aggregate<TAccumulate>(
            this IAsyncEnumerable<TAccumulate> source,
            Func<TAccumulate?, TAccumulate, TAccumulate> func)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

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

        #endregion

        #region Select

        /// <summary>
        /// Projects each element of an asynchronous sequence into a new form.
        /// </summary>
        /// <typeparam name="T">Source element type.</typeparam>
        /// <typeparam name="R">Result element type.</typeparam>
        /// <param name="items">The source sequence.</param>
        /// <param name="selector">A transform function to apply to each element.</param>
        /// <returns>A lazy asynchronous sequence of transformed elements.</returns>
        public static async IAsyncEnumerable<R> Select<T, R>(
            this IAsyncEnumerable<T> items,
            Func<T, R> selector)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            await foreach (var item in items)
            {
                yield return selector(item);
            }
        }

        /// <summary>
        /// Projects each element of an asynchronous sequence into a new form,
        /// incorporating the element's zero-based index.
        /// </summary>
        /// <typeparam name="T">Source element type.</typeparam>
        /// <typeparam name="R">Result element type.</typeparam>
        /// <param name="items">The source sequence.</param>
        /// <param name="selector">
        /// A transform function that receives the element and its index.
        /// </param>
        /// <returns>A lazy asynchronous sequence of transformed elements.</returns>
        public static async IAsyncEnumerable<R> Select<T, R>(
            this IAsyncEnumerable<T> items,
            Func<T, int, R> selector)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            int idx = 0;
            await foreach (var item in items)
            {
                yield return selector(item, idx);
                idx++;
            }
        }

        #endregion

        #region SelectMany

        /// <summary>
        /// Projects each element of an asynchronous sequence to a synchronous <see cref="IEnumerable{T}"/>
        /// and flattens the resulting sequences into one asynchronous sequence.
        /// </summary>
        /// <typeparam name="TSource">Source element type.</typeparam>
        /// <typeparam name="TResult">Result element type.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="selector">A transform function producing an <see cref="IEnumerable{T}"/> for each source element.</param>
        /// <returns>A flattened asynchronous sequence containing the concatenated sub-sequences.</returns>
        public static async IAsyncEnumerable<TResult> SelectMany<TSource, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, IEnumerable<TResult>> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            await foreach (var item in source)
            {
                foreach (var subItem in selector(item))
                {
                    yield return subItem;
                }
            }
        }

        /// <summary>
        /// Projects each element of an asynchronous sequence to another asynchronous sequence
        /// and flattens the nested asynchronous sequences into one.
        /// </summary>
        /// <typeparam name="TSource">Source element type.</typeparam>
        /// <typeparam name="TResult">Result element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="selector">A transform that returns an <see cref="IAsyncEnumerable{T}"/> for each element.</param>
        /// <returns>A flattened asynchronous sequence of all projected elements.</returns>
        public static async IAsyncEnumerable<TResult> SelectMany<TSource, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, IAsyncEnumerable<TResult>> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            await foreach (var item in source)
            {
                await foreach (var subItem in selector(item))
                {
                    yield return subItem;
                }
            }
        }

        /// <summary>
        /// Projects each element of an asynchronous sequence to a synchronous sequence, incorporates the element's index,
        /// and flattens the resulting sequences into one.
        /// </summary>
        /// <typeparam name="TSource">Source element type.</typeparam>
        /// <typeparam name="TResult">Result element type.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="selector">A transform function that receives the element and its index.</param>
        /// <returns>A flattened asynchronous sequence.</returns>
        public static async IAsyncEnumerable<TResult> SelectMany<TSource, TResult>(
            this IAsyncEnumerable<TSource> source,
            Func<TSource, int, IEnumerable<TResult>> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            int index = 0;
            await foreach (var item in source)
            {
                foreach (var subItem in selector(item, index))
                {
                    yield return subItem;
                }
                index++;
            }
        }

        #endregion

        #region Where

        /// <summary>
        /// Filters an asynchronous sequence based on a synchronous predicate.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">A function to test each element for inclusion.</param>
        /// <returns>A lazy sequence of elements that satisfy the predicate.</returns>
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
        /// Filters an asynchronous sequence based on an asynchronous predicate.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">An async function to test each element for inclusion.</param>
        /// <returns>A lazy sequence of elements that satisfy the predicate.</returns>
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
        /// Filters an asynchronous sequence based on a predicate that incorporates the element's index.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">
        /// A function receiving the element and its zero-based index, returning <c>true</c> to include the element.
        /// </param>
        /// <returns>A lazy sequence of filtered elements.</returns>
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

        #endregion

        #region Take / TakeWhile / Range

        /// <summary>
        /// Returns a specified number of contiguous elements from the start of the sequence.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="count">The number of elements to return.</param>
        /// <returns>An asynchronous sequence containing up to <paramref name="count"/> elements.</returns>
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

        /// <summary>
        /// Produces a slice of the sequence, starting at a zero-based index and including a specified number of elements.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="sequence">The source sequence.</param>
        /// <param name="start">The zero-based starting index.</param>
        /// <param name="count">The number of elements to include.</param>
        /// <returns>A sliced asynchronous sequence.</returns>
        /// <remarks>
        /// This is implemented via a <see cref="Range"/> and is lazy.
        /// </remarks>
        public static IAsyncEnumerable<T> Take<T>(this IAsyncEnumerable<T> sequence, int start, int count)
            => sequence.Take(new Range(start, start + count));

        /// <summary>
        /// Returns a slice of the sequence defined by a <see cref="Range"/> with inclusive start and exclusive end semantics.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="sequence">The source sequence.</param>
        /// <param name="range">
        /// A range whose <see cref="Range.Start"/> and <see cref="Range.End"/> must both be from the beginning (not <c>IsFromEnd</c>).
        /// </param>
        /// <returns>The sliced asynchronous sequence.</returns>
        /// <exception cref="ArgumentException">If <paramref name="range"/> uses <c>IsFromEnd</c>.</exception>
        public static async IAsyncEnumerable<T> Take<T>(
            this IAsyncEnumerable<T> sequence,
            Range range)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));

            if (range.Start.IsFromEnd || range.End.IsFromEnd)
                throw new ArgumentException("Range with IsFromEnd not supported for async sequences", nameof(range));

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
        /// Returns elements from a sequence while a synchronous predicate is <c>true</c>; enumeration stops at first failure.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="whilePredicateFunction">Predicate tested for each element; when it returns <c>false</c> the sequence ends.</param>
        /// <returns>A lazy sequence of the matching leading elements.</returns>
        public static async IAsyncEnumerable<T> Take<T>(
            this IAsyncEnumerable<T> source,
            Func<T, bool> whilePredicateFunction)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (whilePredicateFunction == null)
                throw new ArgumentNullException(nameof(whilePredicateFunction));

            await foreach (var item in source)
            {
                if (!whilePredicateFunction(item))
                    break;
                yield return item;
            }
        }

        /// <summary>
        /// Returns elements from a sequence while an asynchronous predicate evaluates to <c>true</c>;
        /// enumeration stops at the first <c>false</c>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="whilePredicateFunction">Asynchronous predicate.</param>
        /// <returns>A lazy sequence of matching leading elements.</returns>
        public static async IAsyncEnumerable<T> Take<T>(
            this IAsyncEnumerable<T> source,
            Func<T, Task<bool>> whilePredicateFunction)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (whilePredicateFunction == null)
                throw new ArgumentNullException(nameof(whilePredicateFunction));

            await foreach (var item in source)
            {
                if (!await whilePredicateFunction(item))
                    break;
                yield return item;
            }
        }

        #endregion

        #region Skip / SkipWhile

        /// <summary>
        /// Bypasses a specified number of elements in an asynchronous sequence and then yields the remaining elements.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="count">Number of elements to skip.</param>
        /// <returns>A lazy sequence beginning after the skipped elements.</returns>
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
        /// Bypasses elements in an asynchronous sequence as long as a condition is <c>true</c>, then yields the remainder.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="whileConditionPredicate">Predicate that determines when to stop skipping.</param>
        /// <returns>A lazy sequence of the remaining elements.</returns>
        public static async IAsyncEnumerable<T> SkipWhile<T>(
            this IAsyncEnumerable<T> source,
            Func<T, bool> whileConditionPredicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (whileConditionPredicate == null)
                throw new ArgumentNullException(nameof(whileConditionPredicate));

            bool yielding = false;
            await foreach (var item in source)
            {
                if (!yielding && !whileConditionPredicate(item))
                    yielding = true;

                if (yielding)
                    yield return item;
            }
        }

        /// <summary>
        /// Bypasses elements in an asynchronous sequence as long as an asynchronous predicate returns <c>true</c>,
        /// then yields the remaining elements.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="whileConditionPredicate">Asynchronous predicate.</param>
        /// <returns>A lazy sequence of the remaining elements.</returns>
        public static async IAsyncEnumerable<T> SkipWhile<T>(
            this IAsyncEnumerable<T> source,
            Func<T, Task<bool>> whileConditionPredicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (whileConditionPredicate == null)
                throw new ArgumentNullException(nameof(whileConditionPredicate));

            bool yielding = false;
            await foreach (var item in source)
            {
                if (!yielding && !await whileConditionPredicate(item))
                    yielding = true;

                if (yielding)
                    yield return item;
            }
        }

        /// <summary>
        /// Bypasses elements in an asynchronous sequence while a predicate (that has access to the element index)
        /// returns <c>true</c>, then yields the remaining elements.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="whileConditionPredicate">Predicate receiving element and index.</param>
        /// <returns>A lazy sequence of the remaining elements.</returns>
        public static async IAsyncEnumerable<T> Skip<T>(
            this IAsyncEnumerable<T> source,
            Func<T, int, bool> whileConditionPredicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (whileConditionPredicate == null)
                throw new ArgumentNullException(nameof(whileConditionPredicate));

            bool yielding = false;
            int index = 0;
            await foreach (var item in source)
            {
                if (!yielding && !whileConditionPredicate(item, index))
                    yielding = true;

                if (yielding)
                    yield return item;

                index++;
            }
        }

        #endregion

        #region Any

        /// <summary>
        /// Determines whether an asynchronous sequence contains any elements.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns><c>true</c> if the sequence contains at least one element; otherwise <c>false</c>.</returns>
        public static async Task<bool> Any<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator();
            return await enumerator.MoveNextAsync();
        }

        /// <summary>
        /// Determines whether any element of an asynchronous sequence satisfies a synchronous predicate.
        /// Enumeration stops early upon the first match.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">A function to test each element.</param>
        /// <returns><c>true</c> if any element satisfies the predicate; otherwise <c>false</c>.</returns>
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
        /// Determines whether any element of an asynchronous sequence satisfies an asynchronous predicate.
        /// Enumeration stops early upon the first match.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">An asynchronous predicate function.</param>
        /// <returns><c>true</c> if any element satisfies the predicate; otherwise <c>false</c>.</returns>
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

        #endregion

        #region First / FirstOrDefault

        /// <summary>
        /// Returns the first element of an asynchronous sequence.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns>The first element.</returns>
        /// <exception cref="InvalidOperationException">If the sequence contains no elements.</exception>
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
        /// Returns the first element of an asynchronous sequence that satisfies a synchronous predicate.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">Synchronous predicate.</param>
        /// <returns>The first matching element.</returns>
        /// <exception cref="InvalidOperationException">If no element satisfies the predicate.</exception>
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
        /// Returns the first element of an asynchronous sequence that satisfies an asynchronous predicate.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">Asynchronous predicate.</param>
        /// <returns>The first matching element.</returns>
        /// <exception cref="InvalidOperationException">If no element satisfies the predicate.</exception>
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

        /// <summary>
        /// Returns the first element of an asynchronous sequence, or the default value if the sequence is empty.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns>The first element, or default.</returns>
        public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator();
            if (await enumerator.MoveNextAsync())
                return enumerator.Current;

            return default;
        }

        /// <summary>
        /// Returns the first element of an asynchronous sequence that satisfies a synchronous predicate,
        /// or the default value if no such element exists.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">Source sequence.</param>
        /// <param name="predicate">Predicate.</param>
        /// <returns>The first matching element, or default.</returns>
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

            return default;
        }

        /// <summary>
        /// Returns the first element of an asynchronous sequence that satisfies an asynchronous predicate,
        /// or the default value if no such element exists.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">Source sequence.</param>
        /// <param name="predicate">Asynchronous predicate.</param>
        /// <returns>The first matching element, or default.</returns>
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

            return default;
        }

        #endregion

        #region Materialization

        /// <summary>
        /// Creates a <see cref="List{T}"/> containing the elements of the asynchronous sequence.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns>A task producing the materialized list.</returns>
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
        /// Creates an array containing the elements of the asynchronous sequence.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns>A task producing the materialized array.</returns>
        public static async Task<T[]> ToArray<T>(this IAsyncEnumerable<T> source)
        {
            var list = await source.ToList();
            return list.ToArray();
        }

        /// <summary>
        /// Creates a <see cref="Dictionary{TKey,TValue}"/> from the asynchronous sequence according to specified key and value selector functions.
        /// </summary>
        /// <typeparam name="T">Source element type.</typeparam>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TValue">Value type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="keySelector">Function extracting the key.</param>
        /// <param name="valueSelector">Function extracting the value.</param>
        /// <param name="comparer">An optional comparer for keys.</param>
        /// <returns>A task whose result is the constructed dictionary.</returns>
        /// <exception cref="ArgumentException">If duplicate keys are encountered.</exception>
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

        /// <summary>
        /// Reconstructs logical lines from a sequence of string slices separated by a sentinel value.
        /// </summary>
        /// <param name="slices">The source slices.</param>
        /// <param name="separator">Separator token indicating line boundaries.</param>
        /// <returns>
        /// A lazy sequence where each element is the concatenation of slices up to (but excluding) the separator.
        /// </returns>
        public static async IAsyncEnumerable<string> ToLines(this IAsyncEnumerable<string> slices, string separator)
        {
            if (slices == null)
                throw new ArgumentNullException(nameof(slices));
            if (separator == null)
                throw new ArgumentNullException(nameof(separator));

            string buffer = "";
            await foreach (var slice in slices)
            {
                if (slice != separator)
                {
                    buffer += slice;
                }
                else
                {
                    yield return buffer;
                    buffer = "";
                }
            }
        }

        #endregion

        #region Buffer / Batch

        /// <summary>
        /// Buffers the source sequence into arrays (batches) of a specified size.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="size">Batch size (must be greater than zero).</param>
        /// <returns>
        /// A sequence of arrays where each array (except possibly the last) has length <paramref name="size"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="size"/> is less than or equal to zero.</exception>
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

        #endregion
    }
}
