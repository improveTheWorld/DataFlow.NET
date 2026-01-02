using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides debugging and inspection helpers for <see cref="IAsyncEnumerable{T}"/> streams.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description><see cref="Spy{T}"/> - writes elements to console while passing through.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="Display"/> - eagerly enumerates and displays elements.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="ToLines"/> - reassembles string slices into logical lines.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// All methods returning <see cref="IAsyncEnumerable{T}"/> are <b>lazy</b> unless noted otherwise.
    /// </para>
    /// <para>
    /// Console output is not thread-safe; interleaving is possible with concurrent consumers.
    /// </para>
    /// </remarks>

    public static class AsyncEnumerableDebuggingExtensions
    {
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
    }
}
