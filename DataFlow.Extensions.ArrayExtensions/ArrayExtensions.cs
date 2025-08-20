using System;
using System.Collections.Generic;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides extension methods for working with array- and collection-like types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The helpers in this type are intentionally minimal and focused on improving
    /// code clarity in frequent usage scenarios. All current methods operate in
    /// <c>O(1)</c> time for standard <see cref="ICollection{T}"/> implementations because
    /// they rely only on the <see cref="ICollection{T}.Count"/> property and do not enumerate
    /// the collection.
    /// </para>
    /// <para>
    /// Methods are pure (side‑effect free) and perform no defensive null checks unless
    /// explicitly stated. This keeps overhead minimal in performance‑sensitive paths.
    /// Callers who require argument validation should perform it externally.
    /// </para>
    /// </remarks>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Gets the zero‑based index of the last element in the collection, or <c>-1</c> if the collection is empty.
        /// </summary>
        /// <typeparam name="T">The element type of the collection.</typeparam>
        /// <param name="collection">The source collection.</param>
        /// <returns>
        /// An <see cref="int"/> representing the last valid index (<c>Count - 1</c>),
        /// or <c>-1</c> when <paramref name="collection"/> contains no elements.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method does not enumerate <paramref name="collection"/>. It reads only
        /// <see cref="ICollection{T}.Count"/>, so it executes in constant time for typical
        /// collection implementations such as <see cref="List{T}"/>, <see cref="T:System.Collections.ObjectModel.Collection{T}"/>,
        /// <see cref="HashSet{T}"/>, etc.
        /// </para>
        /// <para>
        /// A return value of <c>-1</c> is a deliberate design choice allowing concise,
        /// branch-safe patterns when you need to test for a non-empty collection and then
        /// index into it:
        /// </para>
        /// <code language="csharp"><![CDATA[
        /// var idx = items.LastIdx();
        /// if (idx >= 0)
        /// {
        ///     var lastItem = items.ElementAt(idx); // Or items[idx] if it's a List<T>
        /// }
        /// ]]></code>
        /// <para>
        /// No explicit null check is performed: if <paramref name="collection"/> is <c>null</c>,
        /// a <see cref="NullReferenceException"/> will be thrown by the runtime. This matches
        /// the philosophy of lightweight extensions and makes failure modes explicit.
        /// </para>
        /// <para>
        /// Thread-safety: This method is not inherently thread-safe. If another thread
        /// mutates the collection between retrieving the index and using it, an
        /// <see cref="ArgumentOutOfRangeException"/> (for indexers) or logical inconsistency
        /// could occur. Protect concurrent access externally if required.
        /// </para>
        /// </remarks>
        /// <exception cref="NullReferenceException">
        /// Thrown (implicitly) if <paramref name="collection"/> is <c>null</c>.
        /// </exception>
        /// <example>
        /// <code language="csharp"><![CDATA[
        /// var numbers = new List<int> { 10, 20, 30 };
        /// int lastIndex = numbers.LastIdx();   // 2
        /// int last = lastIndex >= 0 ? numbers[lastIndex] : default;
        ///
        /// var empty = new List<int>();
        /// int emptyLast = empty.LastIdx();     // -1
        /// ]]></code>
        /// </example>
        public static int LastIdx<T>(this ICollection<T> collection) => collection.Count - 1;
    }
}
