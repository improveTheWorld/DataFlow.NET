using System.Text;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides extension methods for flattening nested asynchronous and/or synchronous sequences
    /// (<see cref="IAsyncEnumerable{T}"/> and <see cref="IEnumerable{T}"/> variants) into a single
    /// <see cref="IAsyncEnumerable{T}"/> stream.
    /// </summary>
    /// <remarks>
    /// These helpers unify common "flatten" / "SelectMany" scenarios across mixed async/sync nesting:
    /// <list type="bullet">
    ///   <item>
    ///     <description><c>IAsyncEnumerable&lt;IAsyncEnumerable&lt;T&gt;&gt;</c> → async flattening with full streaming on both levels.</description>
    ///   </item>
    ///   <item>
    ///     <description><c>IAsyncEnumerable&lt;IEnumerable&lt;T&gt;&gt;</c> → outer async, inner synchronous collections.</description>
    ///   </item>
    ///   <item>
    ///     <description><c>IEnumerable&lt;IAsyncEnumerable&lt;T&gt;&gt;</c> → outer synchronous collection, inner async streams.</description>
    ///   </item>
    /// </list>
    /// Separator overloads append a delimiter value after each inner sequence, which can be used to
    /// preserve logical boundaries (e.g., reconstruct lines, blocks, or groups after flattening).
    /// All methods are lazy: no enumeration occurs until the returned <see cref="IAsyncEnumerable{T}"/> is consumed.
    /// Cancellation is cooperative (standard <c>await foreach</c> semantics); no explicit token overloads are provided
    /// to keep the API concise—wrap with <c>WithCancellation(token)</c> at the call site if needed.
    /// </remarks>
    public static class AsyncEnumerableFlatteningExtensions
    {
        /// <summary>
        /// Flattens an asynchronously-produced sequence of asynchronous sequences into a single
        /// <see cref="IAsyncEnumerable{T}"/> by fully streaming both levels.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">An outer asynchronous sequence where each element is itself an asynchronous sequence.</param>
        /// <returns>
        /// A lazy, flattened asynchronous sequence yielding elements in the order they appear in each inner sequence,
        /// preserving the outer sequence's order.
        /// </returns>
        /// <remarks>
        /// This behaves like a two-level <c>SelectMany</c> that preserves ordering and does not buffer entire inner
        /// sequences. Each inner sequence is fully enumerated before moving to the next (depth-first).
        /// </remarks>
        public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items)
        {
            await foreach (var seq in items)
            {
                await foreach (var item in seq)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Flattens an asynchronously-produced sequence of synchronous (in-memory) collections into a single
        /// <see cref="IAsyncEnumerable{T}"/>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">An outer asynchronous sequence where each element is a synchronous <see cref="IEnumerable{T}"/>.</param>
        /// <returns>
        /// A lazy asynchronous flattened sequence enumerating each inner collection in order.
        /// </returns>
        /// <remarks>
        /// Suitable when inner collections are already materialized, but the outer sequence is streamed progressively.
        /// Inner enumeration is immediate; only one inner collection resides in memory at a time (beyond its own size).
        /// </remarks>
        public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IEnumerable<T>> items)
        {
            await foreach (var seq in items)
            {
                foreach (var item in seq)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Flattens a synchronous collection of asynchronous sequences into a single
        /// <see cref="IAsyncEnumerable{T}"/>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">A synchronous collection where each element is an asynchronous sequence.</param>
        /// <returns>
        /// A lazy asynchronous flattened sequence. The outer collection is enumerated immediately, and each inner
        /// asynchronous sequence is streamed in order.
        /// </returns>
        /// <remarks>
        /// Use when you have a known, finite set of asynchronous producers. Inner sequences are consumed sequentially.
        /// </remarks>
        public static async IAsyncEnumerable<T> Flatten<T>(this IEnumerable<IAsyncEnumerable<T>> items)
        {
            foreach (var seq in items)
            {
                await foreach (var item in seq)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Flattens an asynchronously-produced sequence of asynchronous sequences, inserting a separator
        /// element after each inner sequence.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">Outer asynchronous sequence of asynchronous inner sequences.</param>
        /// <param name="separator">
        /// A value appended after each fully enumerated inner sequence (including the last). If you do not want a trailing
        /// separator, filter it afterward or post-process accordingly.
        /// </param>
        /// <returns>A flattened asynchronous sequence with separator markers delimiting inner sequence boundaries.</returns>
        /// <remarks>
        /// Common usage: reconstruct original segmentation via grouping on the separator sentinel, or feed into
        /// downstream logic expecting explicit boundaries.
        /// </remarks>
        public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, T separator)
        {
            await foreach (var seq in items)
            {
                await foreach (var item in seq)
                {
                    yield return item;
                }
                yield return separator;
            }
        }

        /// <summary>
        /// Flattens an asynchronously-produced sequence of synchronous collections, inserting a separator
        /// element after each inner collection.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">Outer asynchronous sequence whose elements are synchronous collections.</param>
        /// <param name="separator">Value appended after each inner collection (including the last).</param>
        /// <returns>A flattened asynchronous sequence with separator values marking collection boundaries.</returns>
        /// <remarks>
        /// If you need to omit the final separator, you can post-process with a custom enumerator or a tail-trim filter.
        /// </remarks>
        public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IEnumerable<T>> items, T separator)
        {
            await foreach (var seq in items)
            {
                foreach (var item in seq)
                {
                    yield return item;
                }
                yield return separator;
            }
        }

        /// <summary>
        /// Flattens a synchronous collection of asynchronous sequences, inserting a separator
        /// element after each inner asynchronous sequence.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">Synchronous collection of asynchronous sequences.</param>
        /// <param name="separator">Value appended after each inner sequence (including the last).</param>
        /// <returns>
        /// A lazy flattened asynchronous sequence. The outer collection is enumerated immediately; each inner
        /// async sequence is streamed, followed by a separator value.
        /// </returns>
        /// <remarks>
        /// Ideal when you have a pre-defined set of asynchronous producers and need explicit boundary markers.
        /// </remarks>
        public static async IAsyncEnumerable<T> Flatten<T>(this IEnumerable<IAsyncEnumerable<T>> items, T separator)
        {
            foreach (var seq in items)
            {
                await foreach (var item in seq)
                {
                    yield return item;
                }
                yield return separator;
            }
        }
    }
}
