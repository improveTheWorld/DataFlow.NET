using System.Diagnostics;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides debugging and inspection helpers for <see cref="IEnumerable{T}"/> pipelines.
    /// These helpers are designed to be side-effecting yet still allow fluent chaining.
    /// </summary>
    /// <remarks>
    /// All methods in this class (except <see cref="Display"/> which is terminal / eager) are
    /// <b>lazy</b> and defer execution until the returned enumerable is iterated.
    /// The <c>Spy</c> methods write to <see cref="System.Console"/> as elements flow through
    /// while still yielding each original element (pass-through).
    /// </remarks>
    public static class EnumerableDebuggingExtension
    {
        /// <summary>
        /// Converts a flattened sequence of string "slices" into complete lines using a separator token.
        /// </summary>
        /// <param name="slices">The source sequence of string fragments.</param>
        /// <param name="separator">
        /// The sentinel separator value that denotes the end of a logical line
        /// (e.g., <c>"\n"</c> or any token you used while flattening).
        /// </param>
        /// <returns>
        /// A lazy sequence where each element is the concatenation of consecutive
        /// slices up to (but not including) the separator. The separator itself
        /// is consumed and not present in the output.
        /// </returns>
        /// <remarks>
        /// This method is <b>lazy</b>:
        /// Enumeration occurs only when the result is iterated.
        /// If the sequence does not end with a separator, the trailing buffered
        /// content will be lost (intentional to mirror original behavior). Add a final
        /// separator beforehand if the last line must be captured.
        /// </remarks>
        public static IEnumerable<string> ToLines(this IEnumerable<string> slices, string separator)
        {
            string sum = "";
            foreach (var slice in slices)
            {
                if (slice != separator)
                {
                    sum += slice;
                }
                else
                {
                    yield return sum;
                    sum = "";
                }
            }
        }

        /// <summary>
        /// Default prefix (header) written before a <c>Spy</c> or <c>Display</c> block.
        /// </summary>
        public const string BEFORE = "---------{\n";

        /// <summary>
        /// Default suffix (footer) written after a <c>Spy</c> or <c>Display</c> block.
        /// </summary>
        public const string AFTER = "\n-------}";

        public const string SEPARATOR = "\n";


        /// <summary>
        /// Writes each string item to the console for diagnostic purposes while
        /// passing the original elements through unchanged.
        /// </summary>
        /// <param name="items">The source sequence.</param>
        /// <param name="tag">An optional tag/title written before the block (ignored if null or empty).</param>
        /// <param name="timeStamp">
        /// If <c>true</c>, a timestamp header and elapsed time (ms) footer are emitted.
        /// </param>
        /// <param name="separator">
        /// The string written between elements (defaults to <see cref="SEPARATOR"/>).
        /// </param>
        /// <param name="before">A header block written once before enumeration (defaults to <see cref="BEFORE"/>).</param>
        /// <param name="after">A footer block written once after enumeration (defaults to <see cref="AFTER"/>).</param>
        /// <returns>
        /// A lazy pass-through sequence of the original elements.
        /// </returns>
        /// <remarks>
        /// Equivalent convenience overload of <see cref="Spy{T}(IEnumerable{T}, string, Func{T, string}, bool, string, string, string)"/>
        /// using <c>x =&gt; x</c> as the display formatter.
        /// </remarks>
        public static IEnumerable<string> Spy(
            this IEnumerable<string> items,
            string tag,
            bool timeStamp = false,
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
            => items.Spy<string>(tag, x => x, timeStamp, separator, before, after);

        /// <summary>
        /// Writes each element to the console for diagnostic purposes using a custom formatter
        /// while passing the original elements through unchanged.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">The source sequence.</param>
        /// <param name="tag">An optional tag/title written before the block (ignored if null or empty).</param>
        /// <param name="customDispay">A delegate that formats each element for output.</param>
        /// <param name="timeStamp">
        /// If <c>true</c>, a timestamp header (start time) and elapsed time (ms) footer are emitted.
        /// </param>
        /// <param name="separator">
        /// The string written between elements (defaults to <see cref="SEPARATOR"/>).
        /// </param>
        /// <param name="before">A header block written once before enumeration (defaults to <see cref="BEFORE"/>).</param>
        /// <param name="after">A footer block written once after enumeration (defaults to <see cref="AFTER"/>).</param>
        /// <returns>
        /// A lazy pass-through sequence of the original elements. The sequence is only
        /// enumerated when a downstream operation iterates it.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is <b>lazy</b>; it does not enumerate <paramref name="items"/> until
        /// the returned enumerable is consumed.
        /// </para>
        /// <para>
        /// Thread-safety: Not synchronized. If multiple threads enumerate the returned
        /// sequence concurrently, interleaved console output may result.
        /// </para>
        /// <para>
        /// Performance: Console I/O is slow; this utility is intended only for debugging.
        /// </para>
        /// </remarks>
        public static IEnumerable<T> Spy<T>(
            this IEnumerable<T> items,
            string tag,
            Func<T, string> customDispay,
            bool timeStamp = false,
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
        {
            string startedAt = string.Empty;
            Stopwatch stopwatch = new();
            if (timeStamp)
            {
                DateTime now = DateTime.Now;
                startedAt = $"[{now.Hour}:{now.Minute}:{now.Second}.{now.Millisecond}]";
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }

            Console.WriteLine(startedAt);
            if (!tag.IsNullOrEmpty())
            {
                Console.Write(tag);
            }
            Console.Write(" :");

            Console.Write(before);
            int i = 0;
            foreach (var item in items)
            {
                if (i != 0)
                    Console.Write(separator);

                Console.Write(customDispay(item));
                yield return item;
                i++;
            }

            Console.Write(after);
            if (timeStamp)
            {
                stopwatch.Stop();
                Console.Write($"[{stopwatch.Elapsed.TotalMilliseconds} ms]");
            }
        }

        /// <summary>
        /// Eagerly enumerates the sequence and writes all items to the console.
        /// </summary>
        /// <param name="items">The source sequence of strings.</param>
        /// <param name="tag">
        /// An optional label printed before the block (default = <c>"Displaying"</c>).
        /// Ignored if null or empty.
        /// </param>
        /// <param name="separator">Separator written between items (defaults to <see cref="SEPARATOR"/>).</param>
        /// <param name="before">Header block (defaults to <see cref="BEFORE"/>).</param>
        /// <param name="after">Footer block (defaults to <see cref="AFTER"/>).</param>
        /// <remarks>
        /// This method is <b>eager</b>; it forces enumeration immediately and
        /// is intended purely for diagnostic / logging purposes.
        /// </remarks>
        public static void Display(
            this IEnumerable<string> items,
            string tag = "Displaying",
            string separator = SEPARATOR,
            string before = BEFORE,
            string after = AFTER)
        {
            Console.WriteLine();
            if (!tag.IsNullOrEmpty())
            {
                Console.Write(tag);
            }
            Console.Write(" :");

            Console.Write(before);
            int i = 0;
            foreach (var item in items)
            {
                if (i != 0)
                    Console.Write(separator);
                Console.Write(item);
                i++;
            }
            Console.Write(after);
        }
    }
}