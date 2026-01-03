#nullable enable
using System;
using System.Collections.Generic;

namespace DataFlow;

/// <summary>
/// Provides helper extension methods for working with <see cref="IEnumerator{T}"/> instances
/// in a safe and expressive way.
/// </summary>
/// <remarks>
/// These helpers offer a lighter alternative to manual <c>MoveNext()</c> / <c>Current</c> patterns
/// when iterating enumerators directly (e.g., inside custom iterator pipelines or merging algorithms).
/// <para>
/// They do <b>not</b> modify the lifecycle contract of the underlying enumerator. The caller remains
/// responsible for disposing enumerators that implement <see cref="IDisposable"/>.
/// </para>
/// <para>
/// No defensive <c>null</c> checks are performed—passing a <c>null</c> enumerator will result in a
/// <see cref="NullReferenceException"/> consistent with standard LINQ extension behavior.
/// </para>
/// </remarks>
public static class EnumeratorExtensions
{
    /// <summary>
    /// Attempts to advance the enumerator to the next element and obtains that element
    /// via an <c>out</c> parameter in a single, concise operation.
    /// </summary>
    /// <typeparam name="T">The element type produced by the enumerator.</typeparam>
    /// <param name="enumerator">The source enumerator to advance. Must not be <c>null</c>.</param>
    /// <param name="value">
    /// When this method returns:
    /// <list type="bullet">
    /// <item><description>If the method returns <c>true</c>, contains the element at the new current position.</description></item>
    /// <item><description>If the method returns <c>false</c>, contains <c>default(T)</c> because the end of the sequence was reached.</description></item>
    /// </list>
    /// </param>
    /// <returns>
    /// <c>true</c> if the enumerator successfully advanced and a current element is available;
    /// <c>false</c> if the enumerator is already past the end of the sequence.
    /// </returns>
    /// <remarks>
    /// This is a convenience wrapper around the standard pattern:
    /// <code>
    /// if (enumerator.MoveNext())
    /// {
    ///     var item = enumerator.Current;
    ///     ...
    /// }
    /// </code>
    /// Using <see cref="TryGetNext{T}(IEnumerator{T}, out T)"/> can make loop constructs or manual merge
    /// operations more concise and expressive.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var e = numbers.GetEnumerator();
    /// while (e.TryGetNext(out var n))
    /// {
    ///     Console.WriteLine(n);
    /// }
    /// </code>
    /// </example>
    public static bool TryGetNext<T>(this IEnumerator<T> enumerator, out T? value)
    {
        if (enumerator.MoveNext())
        {
            value = enumerator.Current;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Advances the enumerator and returns the next element if one exists; otherwise returns <c>default(T)</c>.
    /// </summary>
    /// <typeparam name="T">The element type produced by the enumerator.</typeparam>
    /// <param name="enumerator">The source enumerator to advance. Must not be <c>null</c>.</param>
    /// <returns>
    /// The next element in the sequence if available; otherwise <c>default(T)</c> when the end
    /// of the sequence has been reached.
    /// </returns>
    /// <remarks>
    /// This is a convenience method for scenarios where you only care about the next element and
    /// can treat absence as a <c>default</c> sentinel (common in look‑ahead merge algorithms).
    /// <para>
    /// Be cautious when <typeparamref name="T"/> is a reference type and <c>null</c> is also a valid data value,
    /// as the returned <c>default</c> does not distinguish between “end of sequence” and an actual stored <c>null</c>.
    /// In those cases, prefer <see cref="TryGetNext{T}(IEnumerator{T}, out T)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var e = words.GetEnumerator();
    /// for (var next = e.GetNext(); next != null; next = e.GetNext())
    /// {
    ///     Console.WriteLine(next);
    /// }
    /// </code>
    /// </example>
    public static T? GetNext<T>(this IEnumerator<T> enumerator)
    {
        if (enumerator.MoveNext())
        {
            return enumerator.Current;
        }

        return default;
    }
}
