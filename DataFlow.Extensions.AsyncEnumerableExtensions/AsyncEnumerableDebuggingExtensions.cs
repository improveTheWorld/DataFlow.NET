using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides debugging, inspection, and a small set of LINQ-style helper extensions
    /// for <see cref="IAsyncEnumerable{T}"/> streams.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The methods in this class fall into two broad categories:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description><b>Debug / Display helpers</b> (e.g. <see cref="Spy{T}"/>, <see cref="Display"/>) that
    ///     write textual representations of a stream to the console while preserving the
    ///     pass-through behavior (they still yield the original items).</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Functional-style helpers</b> (e.g. <see cref="SelectMany{T, TResult}"/>,
    ///     <see cref="Distinct{T}"/>, <see cref="Concat{T}"/>, <see cref="Append{T}"/>, <see cref="Aggregate{T}"/>)
    ///     that mirror common LINQ concepts for asynchronous sequences.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Unless explicitly stated otherwise, extension methods returning an <see cref="IAsyncEnumerable{T}"/>
    /// are <b>lazy</b>: the supplied sequence is not enumerated and no side-effects occur until the
    /// returned sequence is iterated with <c>await foreach</c> or consumed by a terminal operation.
    /// </para>
    /// <para>
    /// Console output produced by the debugging helpers is not thread-safe against concurrent writers
    /// outside of these methods. If multiple asynchronous sequences emit via <see cref="Spy{T}"/> simultaneously,
    /// interleaving is possible.
    /// </para>
    /// </remarks>
    public static class AsyncEnumerableDebuggingExtensions
    {
        /// <summary>
        /// Default opening delimiter used by <see cref="Spy{T}"/> and <see cref="Display"/>.
        /// </summary>
        public const string BEFORE = "---------{\n";

        /// <summary>
        /// Default closing delimiter used by <see cref="Spy{T}"/> and <see cref="Display"/>.
        /// </summary>
        public const string AFTER = "\n-------}";

        /// <summary>
        /// Default element separator (newline) used by <see cref="Spy{T}"/> and <see cref="Display"/>.
        /// </summary>
        public const string SEPARATOR = "\n";


        /// <summary>
        /// Writes the contents of a string asynchronous sequence to the console in a structured
        /// block while returning a pass-through stream of the original elements.
        /// </summary>
        /// <param name="items">The source asynchronous sequence of strings.</param>
        /// <param name="tag">A label written before the captured block (can be empty or null).</param>
        /// <param name="timeStamp">
        /// If <c>true</c>, a timestamp and elapsed time (upon completion) are displayed.
        /// </param>
        /// <param name="separator">Separator string printed between elements (default: newline).</param>
        /// <param name="before">A preamble or opening delimiter (default: <see cref="BEFORE"/>).</param>
        /// <param name="after">A closing delimiter (default: <see cref="AFTER"/>).</param>
        /// <returns>
        /// A pass-through <see cref="IAsyncEnumerable{T}"/> that yields the original items in their
        /// original order.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method enumerates the source sequence <b>exactly once</b>. It writes each item as it
        /// becomes available. Enumeration is <b>deferred</b> until the returned sequence is iterated.
        /// </para>
        /// <para>
        /// Console color changes are temporary; colors are reset after each call.
        /// </para>
        /// </remarks>
        public static IAsyncEnumerable<string> Spy(
            this IAsyncEnumerable<string> items,
            string tag,
            bool timeStamp = false,
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
        {
            return items.Spy<string>(tag, x => x, timeStamp, separator, before, after);
        }

        /// <summary>
        /// Writes the contents of an asynchronous sequence to the console using a custom
        /// projection while yielding the original items (pass-through).
        /// </summary>
        /// <typeparam name="T">The element type of the sequence.</typeparam>
        /// <param name="items">The source asynchronous sequence.</param>
        /// <param name="tag">A label written before the captured block (can be empty or null).</param>
        /// <param name="customDisplay">A function that converts each element to a display string.</param>
        /// <param name="timeStamp">
        /// If <c>true</c>, prints a timestamp before enumeration begins and, upon completion,
        /// prints elapsed time and item count.
        /// </param>
        /// <param name="separator">Separator string printed between elements (default: newline).</param>
        /// <param name="before">Opening delimiter (default: <see cref="BEFORE"/>).</param>
        /// <param name="after">Closing delimiter (default: <see cref="AFTER"/>).</param>
        /// <returns>
        /// A pass-through <see cref="IAsyncEnumerable{T}"/> that yields the original elements.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Enumeration is deferred; side-effects (console writes) happen only during consumption.
        /// </para>
        /// <para>
        /// For large or high-throughput streams, console I/O will become a bottleneck and may
        /// distort performance measurements.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="customDisplay"/> is <c>null</c>.</exception>
        public static async IAsyncEnumerable<T> Spy<T>(
            this IAsyncEnumerable<T> items,
            string tag,
            Func<T, string> customDisplay,
            bool timeStamp = false,
            string separator = "\n",
            string before = "---------{\n",
            string after = "\n-------}")
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (customDisplay == null) throw new ArgumentNullException(nameof(customDisplay));

            string startedAt = string.Empty;
            Stopwatch stopwatch = new();

            if (timeStamp)
            {
                DateTime now = DateTime.Now;
                startedAt = $"[{now:HH:mm:ss.fff}]";
                stopwatch = Stopwatch.StartNew();
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{startedAt} {tag}");
            Console.ResetColor();
            Console.Write($" :{before}");

            int count = 0;
            await foreach (var item in items)
            {
                if (count > 0) Console.Write(separator);
                Console.Write(customDisplay(item));
                yield return item;
                count++;
            }

            Console.Write(after);

            if (timeStamp)
            {
                stopwatch.Stop();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" [{stopwatch.Elapsed.TotalMilliseconds:F0}ms, {count} items]");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Materializes (enumerates) an asynchronous sequence of strings and writes a
        /// formatted block to the console including indices.
        /// </summary>
        /// <param name="items">The source sequence of strings (nullable entries allowed).</param>
        /// <param name="tag">A descriptive label printed before the block (default: "Displaying").</param>
        /// <param name="separator">Separator printed between items (default: newline).</param>
        /// <param name="before">Opening delimiter (default: <see cref="BEFORE"/>).</param>
        /// <param name="after">Closing delimiter (default: <see cref="AFTER"/>).</param>
        /// <returns>A task that completes when enumeration and console output have finished.</returns>
        /// <remarks>
        /// <para>
        /// This method is a <b>terminal action</b>: it enumerates the entire sequence immediately.
        /// </para>
        /// <para>
        /// Each printed line includes an index: <c>index :  value</c>.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
        public static async Task Display(
            this IAsyncEnumerable<string?> items,
            string tag = "Displaying",
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            Console.WriteLine();
            if (!tag.IsNullOrEmpty())
                Console.Write(tag);
            Console.Write(" :");

            Console.Write(before);
            int i = 0;
            await foreach (var item in items)
            {
                if (i != 0) Console.Write(separator);
                Console.Write($"{i} :  {item}");
                i++;
            }
            Console.Write(after);
        }

        // --------------------------------------------------------------------
        // SELECT MANY
        // --------------------------------------------------------------------

        /// <summary>
        /// Projects each element of the source sequence to an inner asynchronous sequence
        /// and flattens (concatenates) all resulting sequences into a single asynchronous sequence.
        /// </summary>
        /// <typeparam name="T">The type of the source elements.</typeparam>
        /// <typeparam name="TResult">The type of the flattened elements.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="selector">A function that maps each element to an inner <see cref="IAsyncEnumerable{T}"/>.</param>
        /// <returns>
        /// A flattened <see cref="IAsyncEnumerable{T}"/> containing the concatenated results
        /// of invoking <paramref name="selector"/> on each source element.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method enumerates each inner sequence fully before moving to the next source element
        /// (depth-first concatenation). It is lazy: no enumeration occurs until the result is consumed.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="selector"/> is <c>null</c>.</exception>
        public static async IAsyncEnumerable<TResult> SelectMany<T, TResult>(
            this IAsyncEnumerable<T> source,
            Func<T, IAsyncEnumerable<TResult>> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            await foreach (var item in source)
            {
                var inner = selector(item);
                if (inner == null) continue;
                await foreach (var subItem in inner)
                {
                    yield return subItem;
                }
            }
        }

        /// <summary>
        /// Projects each source element to an inner asynchronous sequence and then
        /// combines source and inner elements into a flattened projection.
        /// </summary>
        /// <typeparam name="T">The type of the source elements.</typeparam>
        /// <typeparam name="TCollection">The type of the elements in the inner sequences.</typeparam>
        /// <typeparam name="TResult">The type of the result elements.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="collectionSelector">Function producing an inner asynchronous sequence for each source element.</param>
        /// <param name="resultSelector">Function combining the outer element and an inner element into a result.</param>
        /// <returns>A flattened asynchronous sequence of combined results.</returns>
        /// <remarks>
        /// <para>
        /// Logical counterpart to LINQ's <c>SelectMany</c> with a result selector.
        /// Enumeration is deferred; each inner sequence is processed in turn.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="source"/>, <paramref name="collectionSelector"/>, or
        /// <paramref name="resultSelector"/> is <c>null</c>.
        /// </exception>
        public static async IAsyncEnumerable<TResult> SelectMany<T, TCollection, TResult>(
            this IAsyncEnumerable<T> source,
            Func<T, IAsyncEnumerable<TCollection>> collectionSelector,
            Func<T, TCollection, TResult> resultSelector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (collectionSelector == null)
                throw new ArgumentNullException(nameof(collectionSelector));
            if (resultSelector == null)
                throw new ArgumentNullException(nameof(resultSelector));

            await foreach (var item in source)
            {
                var inner = collectionSelector(item);
                if (inner == null) continue;
                await foreach (var subItem in inner)
                {
                    yield return resultSelector(item, subItem);
                }
            }
        }

        // --------------------------------------------------------------------
        // DISTINCT
        // --------------------------------------------------------------------

        /// <summary>
        /// Returns distinct elements from an asynchronous sequence by using an optional
        /// equality comparer to compare values.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="comparer">
        /// An <see cref="IEqualityComparer{T}"/> to compare values, or <c>null</c> to use
        /// <see cref="EqualityComparer{T}.Default"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that yields distinct elements while preserving
        /// first-seen order.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Maintains an in-memory <see cref="HashSet{T}"/> of seen elements; for very large
        /// or unbounded streams, memory use grows proportionally to the number of unique items.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        public static async IAsyncEnumerable<T> Distinct<T>(
            this IAsyncEnumerable<T> source,
            IEqualityComparer<T>? comparer = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var seen = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);

            await foreach (var item in source)
            {
                if (seen.Add(item))
                    yield return item;
            }
        }

        // --------------------------------------------------------------------
        // CONCAT
        // --------------------------------------------------------------------

        /// <summary>
        /// Concatenates two asynchronous sequences (first completely, then second).
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="first">The first source sequence.</param>
        /// <param name="second">The second sequence whose elements follow the first sequence.</param>
        /// <returns>
        /// A concatenated asynchronous sequence containing all elements of <paramref name="first"/>
        /// followed by all elements of <paramref name="second"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Lazy: enumeration of the second sequence does not start until the first completes.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="first"/> or <paramref name="second"/> is <c>null</c>.</exception>
        public static async IAsyncEnumerable<T> Concat<T>(
            this IAsyncEnumerable<T> first,
            IAsyncEnumerable<T> second)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first));
            if (second == null)
                throw new ArgumentNullException(nameof(second));

            await foreach (var item in first)
            {
                yield return item;
            }

            await foreach (var item in second)
            {
                yield return item;
            }
        }

        // --------------------------------------------------------------------
        // APPEND / PREPEND
        // --------------------------------------------------------------------

        /// <summary>
        /// Appends a single element to the end of the asynchronous sequence.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="element">The element to append.</param>
        /// <returns>
        /// A new sequence that yields all elements of <paramref name="source"/> followed by <paramref name="element"/>.
        /// </returns>
        /// <remarks>
        /// Lazy: enumeration defers until consumed.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        public static async IAsyncEnumerable<T> Append<T>(
            this IAsyncEnumerable<T> source,
            T element)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await foreach (var item in source)
            {
                yield return item;
            }

            yield return element;
        }

        /// <summary>
        /// Prepends a single element to the beginning of the asynchronous sequence.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="element">The element to prepend.</param>
        /// <returns>
        /// A new sequence that yields <paramref name="element"/> first, then all elements of <paramref name="source"/>.
        /// </returns>
        /// <remarks>
        /// Lazy: enumeration of <paramref name="source"/> occurs only after yielding the prepended element.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        public static async IAsyncEnumerable<T> Prepend<T>(
            this IAsyncEnumerable<T> source,
            T element)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            yield return element;

            await foreach (var item in source)
            {
                yield return item;
            }
        }

        // --------------------------------------------------------------------
        // AGGREGATE
        // --------------------------------------------------------------------

        /// <summary>
        /// Applies an accumulator function over an asynchronous sequence, producing a single result.
        /// </summary>
        /// <typeparam name="T">The element type and accumulator type.</typeparam>
        /// <param name="source">The source asynchronous sequence.</param>
        /// <param name="func">A function that combines the current accumulated value and the next element.</param>
        /// <returns>A task producing the final accumulated value.</returns>
        /// <remarks>
        /// <para>
        /// Uses the first element as the initial accumulator. If the sequence is empty,
        /// an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// <para>
        /// This method enumerates the sequence eagerly (terminal operation).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="func"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="source"/> is empty.</exception>
        public static async Task<T> Aggregate<T>(
            this IAsyncEnumerable<T> source,
            Func<T, T, T> func)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            await using var enumerator = source.GetAsyncEnumerator();

            if (!await enumerator.MoveNextAsync())
                throw new InvalidOperationException("Sequence contains no elements");

            T result = enumerator.Current;
            while (await enumerator.MoveNextAsync())
            {
                result = func(result, enumerator.Current);
            }

            return result;
        }
    }
}