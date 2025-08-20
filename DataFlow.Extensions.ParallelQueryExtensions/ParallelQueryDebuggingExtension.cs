using System.Diagnostics;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides debugging and diagnostic extension methods for <see cref="ParallelQuery{TSource}"/> sequences.
    /// </summary>
    /// <remarks>
    /// These helpers are intended for ad‑hoc inspection during development or troubleshooting.
    /// They introduce side effects (console writes) and should generally be removed or guarded
    /// by conditional compilation for production scenarios.
    /// <para>
    /// Thread Safety: Console output is serialized via an internal lock to prevent interleaving
    /// between parallel worker threads. The underlying data sequence itself is not modified.
    /// </para>
    /// </remarks>
    public static class ParallelQueryDebuggingExtension
    {
        /// <summary>
        /// Default opening delimiter written before items when using <see cref="Spy{T}"/> or <see cref="Display"/>.
        /// </summary>
        public const string BEFORE = "---------{\n";

        /// <summary>
        /// Default closing delimiter written after items when using <see cref="Spy{T}"/> or <see cref="Display"/>.
        /// </summary>
        public const string AFTER = "\n-------}";

        /// <summary>
        /// Default element separator used between displayed items.
        /// </summary>
        public const string SEPARATOR = "\n";

        private static readonly object _consoleLock = new object();

        /// <summary>
        /// Writes a diagnostic representation of a parallel string sequence to the console as it is enumerated,
        /// returning a pass-through sequence that preserves the original query semantics.
        /// </summary>
        /// <param name="items">The source parallel sequence.</param>
        /// <param name="tag">
        /// An optional label written once before enumeration begins. If <c>null</c> or empty, no tag is written.
        /// </param>
        /// <param name="timeStamp">
        /// If <c>true</c>, the wall-clock start time (HH:mm:ss.fff) is printed before the first item. The elapsed duration
        /// is not printed here (see remarks below).
        /// </param>
        /// <param name="separator">Separator written between successive items. Defaults to <see cref="SEPARATOR"/>.</param>
        /// <param name="before">
        /// Opening delimiter written once before any items (after the tag). Defaults to <see cref="BEFORE"/>.
        /// </param>
        /// <param name="after">
        /// Closing delimiter (currently unused in the pass-through variant; retained for future extensibility).
        /// Defaults to <see cref="AFTER"/>.
        /// </param>
        /// <returns>
        /// A pass-through <see cref="ParallelQuery{TSource}"/> whose enumeration produces the original elements
        /// while emitting side-effect console output.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This overload delegates to the generic <see cref="Spy{T}(ParallelQuery{T}, string, System.Func{T,string}, bool, string, string, string)"/>
        /// using an identity formatter (<c>x =&gt; x</c>).
        /// </para>
        /// <para>
        /// Deferred Execution: No console output occurs until the returned sequence is enumerated (e.g., via
        /// a terminal operation such as <c>ToArray()</c>, <c>ForAll</c>, <c>Sum()</c>, etc.).
        /// </para>
        /// <para>
        /// Footer Omission: To avoid prematurely forcing materialization (and potentially altering the performance
        /// characteristics of the PLINQ query), the current implementation omits writing the <paramref name="after"/>
        /// delimiter. This design prevents the necessity of wrapping/consuming the sequence internally.
        /// </para>
        /// <para>
        /// Concurrency: Each item’s formatted representation is written under a lock to avoid interleaving; however,
        /// ordering is not guaranteed because parallel operations may complete out-of-order.
        /// </para>
        /// </remarks>
        public static ParallelQuery<string> Spy(
            this ParallelQuery<string> items,
            string tag,
            bool timeStamp = false,
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
            => items.Spy(tag, x => x, timeStamp, separator, before, after);

        /// <summary>
        /// Writes a diagnostic representation of a parallel sequence to the console as it is enumerated,
        /// returning a pass-through sequence that preserves the original query semantics.
        /// </summary>
        /// <typeparam name="T">The element type of the parallel sequence.</typeparam>
        /// <param name="items">The source parallel sequence.</param>
        /// <param name="tag">
        /// An optional label written once before enumeration begins. If <c>null</c> or empty, no tag is written.
        /// </param>
        /// <param name="customDisplay">
        /// A projection that converts each element into its textual representation for debugging output.
        /// Must be non-null.
        /// </param>
        /// <param name="timeStamp">
        /// If <c>true</c>, the wall-clock start time (HH:mm:ss.fff) is printed before the first item.
        /// The elapsed duration and footer are intentionally not printed to avoid consuming the query.
        /// </param>
        /// <param name="separator">Separator written between successive items. Defaults to <see cref="SEPARATOR"/>.</param>
        /// <param name="before">
        /// Opening delimiter written once before any items (after the tag). Defaults to <see cref="BEFORE"/>.
        /// </param>
        /// <param name="after">
        /// Closing delimiter (currently unused in this implementation; see remarks). Defaults to <see cref="AFTER"/>.
        /// </param>
        /// <returns>
        /// A pass-through <see cref="ParallelQuery{TSource}"/> whose enumeration produces the original elements
        /// while emitting side-effect console output using <paramref name="customDisplay"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is <b>lazy</b>. It adds side-effecting transformations (writes to the console) without
        /// forcing immediate execution. Items are only processed when a terminal PLINQ operation runs.
        /// </para>
        /// <para>
        /// Ordering: The output order corresponds to the order in which individual partitions produce results,
        /// not necessarily the original source order. To obtain deterministic ordered output, apply <c>.AsOrdered()</c>
        /// to the PLINQ query before calling <c>Spy</c>.
        /// </para>
        /// <para>
        /// Footer Limitation: The <paramref name="after"/> delimiter is not written. Implementing a guaranteed footer
        /// would require consuming the sequence or wrapping it in a custom enumerator that coordinates completion.
        /// The current approach ensures the returned query remains a faithful pass-through.
        /// </para>
        /// <para>
        /// Thread Safety: Console writes are serialized via a lock; the transformation itself is safe to attach
        /// to any PLINQ pipeline. Avoid expensive operations inside <paramref name="customDisplay"/> to prevent
        /// degrading parallel performance.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="customDisplay"/> is null.</exception>
        public static ParallelQuery<T> Spy<T>(
            this ParallelQuery<T> items,
            string tag,
            Func<T, string> customDisplay,
            bool timeStamp = false,
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
        {
            if (customDisplay == null)
                throw new ArgumentNullException(nameof(customDisplay));

            var stopwatch = timeStamp ? Stopwatch.StartNew() : null;
            var startTime = timeStamp ? DateTime.Now : default;
            var count = 0;

            lock (_consoleLock)
            {
                if (timeStamp)
                    Console.WriteLine($"[{startTime:HH:mm:ss.fff}]");

                if (!string.IsNullOrEmpty(tag))
                    Console.Write($"{tag} :");

                Console.Write(before);
            }

            // Pass-through transformation; no materialization.
            var spiedItems = items.Select(item =>
            {
                var display = customDisplay(item);
                lock (_consoleLock)
                {
                    if (Interlocked.Increment(ref count) > 1)
                        Console.Write(separator);
                    Console.Write(display);
                }
                return item;
            });

            // Footer intentionally omitted – see remarks above.
            return spiedItems;
        }

        /// <summary>
        /// Eagerly materializes a parallel sequence of strings and writes them to the console
        /// with formatting delimiters and optional tagging.
        /// </summary>
        /// <param name="items">The source parallel string sequence.</param>
        /// <param name="tag">
        /// An optional label written before the formatted block. Defaults to "Displaying".
        /// If <c>null</c> or empty, no tag label is written.
        /// </param>
        /// <param name="separator">Separator inserted between items. Defaults to <see cref="SEPARATOR"/>.</param>
        /// <param name="before">
        /// Opening delimiter written before the first item (after tag). Defaults to <see cref="BEFORE"/>.
        /// </param>
        /// <param name="after">
        /// Closing delimiter written after the final item. Defaults to <see cref="AFTER"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// This is an <b>eager</b> terminal operation: it forces the parallel query to execute by calling
        /// <see cref="ParallelEnumerable.ToArray{TSource}(ParallelQuery{TSource})"/>. All results are buffered
        /// before being written. For extremely large result sets this may incur significant memory usage.
        /// </para>
        /// <para>
        /// Ordering: The resulting array preserves the PLINQ result ordering semantics in effect.
        /// If deterministic ordering is required, ensure the upstream query uses <c>.AsOrdered()</c>.
        /// </para>
        /// </remarks>
        public static void Display(
            this ParallelQuery<string> items,
            string tag = "Displaying",
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
        {
            Console.WriteLine();
            if (!string.IsNullOrEmpty(tag))
                Console.Write($"{tag} :");

            Console.Write(before);
            var itemsArray = items.ToArray();
            for (int i = 0; i < itemsArray.Length; i++)
            {
                if (i > 0) Console.Write(separator);
                Console.Write(itemsArray[i]);
            }
            Console.Write(after);
        }
    }
}