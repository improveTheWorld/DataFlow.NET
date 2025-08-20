namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides extension methods for flattening sequences of sequences (<see cref="IEnumerable{T}"/> of <see cref="IEnumerable{T}"/>).
    /// </summary>
    /// <remarks>
    /// These methods are implemented using <c>yield return</c> and are therefore fully lazy:
    /// the inner sequences are only enumerated as the resulting flattened sequence is iterated.
    /// <para>
    /// No defensive copying is performed; if any inner sequence is deferred or depends on external mutable state,
    /// the flattened result will reflect that state at enumeration time.
    /// </para>
    /// <para>
    /// If the outer <paramref name="items"/> or any inner sequence contains <c>null</c>, a <see cref="NullReferenceException"/>
    /// may occur during enumeration. Callers should validate inputs if this is a concern.
    /// </para>
    /// </remarks>
    public static class EnumerableFlatteningExtensions
    {
        /// <summary>
        /// Flattens a sequence of sequences into a single, concatenated sequence.
        /// </summary>
        /// <typeparam name="T">The element type of the inner sequences.</typeparam>
        /// <param name="items">The outer sequence containing inner sequences to flatten.</param>
        /// <returns>
        /// A lazy <see cref="IEnumerable{T}"/> that yields each element of each inner sequence in order.
        /// </returns>
        /// <example>
        /// <code>
        /// var nested = new List&lt;IEnumerable&lt;int&gt;&gt;
        /// {
        ///     new [] { 1, 2 },
        ///     new [] { 3 },
        ///     new [] { 4, 5 }
        /// };
        ///
        /// // Result: 1, 2, 3, 4, 5
        /// var flat = nested.Flatten();
        /// </code>
        /// </example>
        /// <remarks>
        /// This method does not insert any separators or delimiters between inner sequences.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown lazily (during enumeration) if <paramref name="items"/> is <c>null</c>.
        /// </exception>
        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            foreach (var seq in items)
            {
                if (seq == null) continue; // Optionally skip null inner sequences; remove if you prefer a hard fail.
                foreach (var item in seq)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Flattens a sequence of sequences into a single sequence, inserting a separator value
        /// after each inner sequence.
        /// </summary>
        /// <typeparam name="T">The element type of the inner sequences and separator.</typeparam>
        /// <param name="items">The outer sequence containing inner sequences to flatten.</param>
        /// <param name="separator">
        /// The value to yield after each inner sequence (including after the last one).
        /// </param>
        /// <returns>
        /// A lazy <see cref="IEnumerable{T}"/> that yields all elements of each inner sequence in order,
        /// followed by the specified <paramref name="separator"/> after every inner sequence.
        /// </returns>
        /// <example>
        /// <code>
        /// var nested = new List&lt;IEnumerable&lt;string&gt;&gt;
        /// {
        ///     new [] { "a", "b" },
        ///     new [] { "c" }
        /// };
        ///
        /// // Result sequence: "a", "b", "|", "c", "|"
        /// var flat = nested.Flatten("|");
        /// </code>
        /// </example>
        /// <remarks>
        /// If you do not want a trailing separator after the last inner sequence,
        /// post-process the result (e.g., <c>.SkipLast(1)</c> in .NET 6+) or write a custom variant.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown lazily (during enumeration) if <paramref name="items"/> is <c>null</c>.
        /// </exception>
        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items, T separator)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            foreach (var seq in items)
            {
                if (seq != null)
                {
                    foreach (var item in seq)
                    {
                        yield return item;
                    }
                }
                yield return separator;
            }
        }
    }
}
