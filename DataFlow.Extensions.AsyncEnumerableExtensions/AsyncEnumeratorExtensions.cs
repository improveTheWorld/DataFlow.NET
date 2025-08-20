// ReSharper disable PossibleMultipleEnumeration
// --------------------------------------------------------------------------------------
// File: AsyncEnumeratorExtensions.cs
// Project: DataFlow.Extensions
// Description:
//     XML-documented helper extensions for IAsyncEnumerator<T>. These utilities provide
//     safer and more expressive patterns for advancing asynchronous enumerators,
//     mirroring common "TryGet*" and "Get*" idioms familiar from synchronous APIs.
//
//     NOTE:
//     These helpers intentionally do NOT dispose the enumerator. The caller owns the
//     lifetime of the enumerator (typically acquired via GetAsyncEnumerator and used
//     inside an await using scope, or indirectly through await foreach).
// --------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides convenience extension methods for working directly with <see cref="IAsyncEnumerator{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These helpers are useful when you need manual, imperative control over asynchronous
    /// iteration rather than using <c>await foreach</c>. Typical scenarios include:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Peeking or probing the next value in a sequence.</description></item>
    ///   <item><description>Manually interleaving multiple asynchronous enumerators.</description></item>
    ///   <item><description>Building custom combinators or control-flow abstractions.</description></item>
    /// </list>
    /// <para>
    /// The methods here do <b>not</b> dispose the enumerator. It is the caller's responsibility
    /// to dispose it (usually via an <c>await using</c> statement) when finished.
    /// </para>
    /// </remarks>
    public static class AsyncEnumeratorExtensions
    {
        /// <summary>
        /// Attempts to advance the asynchronous enumerator and retrieve the next element.
        /// </summary>
        /// <typeparam name="T">The element type produced by the enumerator.</typeparam>
        /// <param name="enumerator">The asynchronous enumerator to advance.</param>
        /// <returns>
        /// A task producing a tuple where:
        /// <list type="bullet">
        ///   <item><description><c>Item1</c> (<c>bool</c>): <c>true</c> if the enumerator advanced and a value is available; otherwise <c>false</c>.</description></item>
        ///   <item><description><c>Item2</c> (<c>T?</c>): The current value if <c>Item1</c> is <c>true</c>; otherwise <c>default</c>.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This is the asynchronous analogue of the synchronous <c>IEnumerator.MoveNext()</c> +
        /// value retrieval pattern, combined into a single, allocation-free (beyond the tuple) result.
        /// </para>
        /// <para>
        /// No exception is thrown when the end of the sequence is reached; instead <c>(false, default)</c>
        /// is returned, making it suitable for "try-get" consumption loops.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// await using var e = source.GetAsyncEnumerator();
        /// while (true)
        /// {
        ///     var (hasValue, value) = await e.TryGetNext();
        ///     if (!hasValue) break;
        ///     Console.WriteLine(value);
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerator"/> is <c>null</c>.</exception>
        public static async Task<(bool hasValue, T? value)> TryGetNext<T>(this IAsyncEnumerator<T> enumerator)
        {
            if (enumerator == null)
                throw new ArgumentNullException(nameof(enumerator));

            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                return (true, enumerator.Current);
            }
            return (false, default);
        }

        /// <summary>
        /// Advances the asynchronous enumerator and returns the next element, or <c>default</c> if the sequence has ended.
        /// </summary>
        /// <typeparam name="T">The element type produced by the enumerator.</typeparam>
        /// <param name="enumerator">The asynchronous enumerator to advance.</param>
        /// <returns>
        /// A task whose result is:
        /// <list type="bullet">
        ///   <item><description>The next element in the sequence if available.</description></item>
        ///   <item><description><c>default(T)</c> if the enumerator has reached the end of the sequence.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is similar to <see cref="TryGetNext{T}"/> but returns only the value, using <c>default</c>
        /// to indicate the absence of further elements. For reference types, a <c>null</c> result is ambiguous
        /// (it may mean "no more items" or "the next item is actually null"). If you need to distinguish those
        /// cases, prefer <see cref="TryGetNext{T}"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// await using var e = source.GetAsyncEnumerator();
        /// T? next;
        /// while ((next = await e.GetNext()) is not null)
        /// {
        ///     Console.WriteLine(next);
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerator"/> is <c>null</c>.</exception>
        public static async Task<T?> GetNext<T>(this IAsyncEnumerator<T> enumerator)
        {
            if (enumerator == null)
                throw new ArgumentNullException(nameof(enumerator));

            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                return enumerator.Current;
            }
            return default;
        }
    }
}
