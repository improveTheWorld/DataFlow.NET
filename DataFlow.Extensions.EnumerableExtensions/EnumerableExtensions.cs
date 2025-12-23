using System.Text;

namespace DataFlow.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="IEnumerable{T}"/> sequences,
/// including ordered merging, conditional early termination (<c>Until</c>),
/// side-effect iteration helpers (<c>ForEach</c>, <c>Do</c>), string building utilities,
/// and null / emptiness checks.
/// </summary>
/// <remarks>
/// All sequence-returning methods are lazy unless explicitly stated (i.e., they
/// defer execution until the returned sequence is enumerated). Methods that
/// return <see langword="void"/> and enumerate internally (such as <see cref="Do{T}(IEnumerable{T})"/>)
/// are eager.
/// </remarks>
public static class EnumerableExtensions
{
    /// <summary>
    /// Merges two already-ordered sequences into a single ordered sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequences.</typeparam>
    /// <param name="first">The first ordered source sequence. May be <c>null</c> (treated as empty).</param>
    /// <param name="second">The second ordered source sequence. May be <c>null</c> (treated as empty).</param>
    /// <param name="isFirstLessThanOrEqualToSecond">
    /// A comparison delegate that returns <c>true</c> when the current element of
    /// <paramref name="first"/> should precede (or is equal to) the current element of
    /// <paramref name="second"/> in the merged ordering.
    /// </param>
    /// <returns>
    /// A lazy sequence that yields elements from <paramref name="first"/> and <paramref name="second"/>
    /// in non‑decreasing order according to <paramref name="isFirstLessThanOrEqualToSecond"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs a standard merge (like the merge step of merge sort) assuming
    /// both inputs are individually ordered under the same ordering relation embodied by
    /// <paramref name="isFirstLessThanOrEqualToSecond"/>.
    /// </para>
    /// <para>
    /// If one sequence is exhausted before the other, the remainder of the non‑exhausted sequence
    /// is yielded as-is. If one (or both) sequences is <c>null</c>, it is treated as empty.
    /// </para>
    /// <para>
    /// This method does not attempt to validate that the inputs are ordered; undefined behavior
    /// (unsorted output) will result if they are not.
    /// </para>
    /// <para>Deferred execution &amp; streaming: elements are yielded as soon as they can be compared.</para>
    /// <para>Complexity: O(n + m) comparisons and yields, where n and m are the lengths of the inputs.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="isFirstLessThanOrEqualToSecond"/> is <c>null</c>.</exception>
    public static IEnumerable<T> MergeOrdered<T>(
        this IEnumerable<T> first,
        IEnumerable<T> second,
        Func<T, T, bool> isFirstLessThanOrEqualToSecond)
    {
        if (isFirstLessThanOrEqualToSecond == null)
            throw new ArgumentNullException(nameof(isFirstLessThanOrEqualToSecond));

        using var enum1 = first?.GetEnumerator();
        using var enum2 = second?.GetEnumerator();

        bool hasNext1 = enum1?.MoveNext() ?? false;
        bool hasNext2 = enum2?.MoveNext() ?? false;

        while (hasNext1 && hasNext2)
        {
            if (isFirstLessThanOrEqualToSecond(enum1!.Current, enum2!.Current))
            {
                yield return enum1.Current;
                hasNext1 = enum1.MoveNext();
            }
            else
            {
                yield return enum2.Current;
                hasNext2 = enum2.MoveNext();
            }
        }
        // Drain remaining elements from whichever sequence still has items
        // Important: yield the current element first (it was compared but not yet yielded)
        if (hasNext1)
        {
            yield return enum1!.Current;
            while (enum1.MoveNext())
                yield return enum1.Current;
        }
        else if (hasNext2)
        {
            yield return enum2!.Current;
            while (enum2.MoveNext())
                yield return enum2.Current;
        }
    }

    /// <summary>
    /// Returns a contiguous slice of the sequence starting at a zero-based <paramref name="start"/> index
    /// spanning <paramref name="count"/> elements.
    /// </summary>
    /// <typeparam name="T">Sequence element type.</typeparam>
    /// <param name="sequence">The source sequence.</param>
    /// <param name="start">Zero-based starting index (must be non-negative).</param>
    /// <param name="count">Number of elements to take (must be non-negative).</param>
    /// <returns>
    /// A lazy sequence representing the requested slice.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is syntactic sugar over <c>sequence.Skip(start).Take(count)</c>. It uses a <see cref="Range"/>
    /// internally to preserve readability. Enumeration stops early if the sequence ends before
    /// <paramref name="count"/> elements are yielded.
    /// </para>
    /// <para>
    /// IMPORTANT: This implementation currently uses <c>new Range(start, start + count - 1)</c>.
    /// <b>If you intend the end index to be exclusive, adjust accordingly.</b>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="start"/> or <paramref name="count"/> is negative.</exception>
    public static IEnumerable<T> Take<T>(this IEnumerable<T> sequence, int start, int count)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return sequence
            .Skip(start)
            .Take(count);
    }

    /// <summary>
    /// Enumerates items until the supplied <paramref name="stopCondition"/> delegate returns <c>true</c>.
    /// The element for which the stop condition becomes <c>true</c> is included in the output.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">The source sequence.</param>
    /// <param name="stopCondition">A parameterless function evaluated after each yield; when it returns <c>true</c>, enumeration ends.</param>
    /// <returns>A lazy sequence that terminates when <paramref name="stopCondition"/> returns <c>true</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stopCondition"/> is <c>null</c>.</exception>
    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<bool> stopCondition)
    {
        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition));

        foreach (var item in items)
        {
            yield return item;
            if (stopCondition())
                break;
        }
    }

    /// <summary>
    /// Enumerates items until the element-level predicate returns <c>true</c>.
    /// The element that satisfies the predicate is included in the output.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">The source sequence.</param>
    /// <param name="stopCondition">A predicate evaluated against each element after yielding it.</param>
    /// <returns>A lazy sequence ending (inclusive) with the first element for which <paramref name="stopCondition"/> returns <c>true</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stopCondition"/> is <c>null</c>.</exception>
    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, bool> stopCondition)
    {
        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition));

        foreach (var item in items)
        {
            yield return item;
            if (stopCondition(item))
                break;
        }
    }

    /// <summary>
    /// Enumerates items until the index-aware predicate returns <c>true</c>.
    /// The element that satisfies the predicate is included in the result.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">The source sequence.</param>
    /// <param name="stopCondition">
    /// A predicate receiving the current element and its zero-based index (starting at 0).
    /// When it returns <c>true</c>, enumeration stops (after yielding that element).
    /// </param>
    /// <returns>A lazy sequence that terminates when <paramref name="stopCondition"/> returns <c>true</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stopCondition"/> is <c>null</c>.</exception>
    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, int, bool> stopCondition)
    {
        if (stopCondition == null)
            throw new ArgumentNullException(nameof(stopCondition));

        int index = 0;
        foreach (var item in items)
        {
            yield return item;
            if (stopCondition(item, index++))
                break;
        }
    }

    /// <summary>
    /// Enumerates the sequence up to and including the element at <paramref name="lastItemIdx"/>.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source sequence.</param>
    /// <param name="lastItemIdx">
    /// Zero-based inclusive index of the final element to yield. If the sequence is shorter,
    /// enumeration ends naturally.
    /// </param>
    /// <returns>A lazy sequence truncated after the specified index.</returns>
    public static IEnumerable<T> Until<T>(this IEnumerable<T> items, int lastItemIdx)
    {
        if (lastItemIdx < 0)
            yield break;

        int index = 0;
        foreach (var item in items)
        {
            yield return item;
            if (lastItemIdx == index++)
                break;
        }
    }

    /// <summary>
    /// Executes a side-effect <paramref name="action"/> for each element while preserving
    /// the original sequence (pass-through). The action receives the element and its index.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source sequence.</param>
    /// <param name="action">Side-effect to execute per element (index provided).</param>
    /// <returns>A lazy sequence yielding the original elements unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> or <paramref name="items"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Evaluation is deferred; actions run only during enumeration. Index counting is zero-based.
    /// </remarks>
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T, int> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));

        return items.Select((x, idx) =>
        {
            action(x, idx);
            return x;
        });
    }

    /// <summary>
    /// Executes a side-effect <paramref name="action"/> for each element while preserving
    /// the original sequence (pass-through).
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source sequence.</param>
    /// <param name="action">Side-effect to execute per element.</param>
    /// <returns>A lazy sequence yielding original elements unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> or <paramref name="items"/> is <c>null</c>.</exception>
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));

        return items.Select(x =>
        {
            action(x);
            return x;
        });
    }

    /// <summary>
    /// Forces enumeration of the sequence discarding all elements (a no-op over each item).
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source sequence (fully enumerated and ignored).</param>
    /// <remarks>
    /// <para>Eager terminal operation. Primarily used to force evaluation of a pipeline for side-effects embedded upstream.</para>
    /// <para>Equivalent to: <c>foreach (var _ in items) { }</c></para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
    public static void Do<T>(this IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        foreach (var _ in items) { /* intentionally empty */ }
    }

    /// <summary>
    /// Forces enumeration of the sequence, executing an action for each element.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source sequence.</param>
    /// <param name="action">Action to execute for each element.</param>
    /// <remarks>
    /// <para>Eager terminal operation. Equivalent to <c>items.ForEach(action).Do()</c>.</para>
    /// <para>Combines side-effect execution with terminal consumption in a single call.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static void Do<T>(this IEnumerable<T> items, Action<T> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));
        foreach (var item in items) { action(item); }
    }

    /// <summary>
    /// Forces enumeration of the sequence, executing an indexed action for each element.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source sequence.</param>
    /// <param name="action">Action to execute for each element, receiving the element and its zero-based index.</param>
    /// <remarks>
    /// <para>Eager terminal operation. Equivalent to <c>items.ForEach(action).Do()</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public static void Do<T>(this IEnumerable<T> items, Action<T, int> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));
        int index = 0;
        foreach (var item in items) { action(item, index++); }
    }

    /// <summary>
    /// Builds a composite string from the sequence of strings using a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="items">Source string sequence.</param>
    /// <param name="str">
    /// Optional existing <see cref="StringBuilder"/> to append into. If <c>null</c>, a new instance is created.
    /// </param>
    /// <param name="separator">Separator inserted between elements (ignored before the first element).</param>
    /// <param name="before">A prefix appended before any elements (skipped if <c>null</c> or empty).</param>
    /// <param name="after">A suffix appended after all elements (skipped if <c>null</c> or empty).</param>
    /// <returns>The (possibly supplied) <see cref="StringBuilder"/> containing the assembled text.</returns>
    /// <remarks>
    /// Eager: enumerates the full sequence. Safe for moderate-size sequences; for very large datasets, consider streaming alternatives.
    /// </remarks>
    public static StringBuilder BuildString(
        this IEnumerable<string> items,
        StringBuilder? str = null,
        string separator = ", ",
        string before = "{",
        string after = "}")
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        str ??= new StringBuilder();

        if (!before.IsNullOrEmpty())
            str.Append(before);

        items.ForEach((x, idx) =>
        {
            if (idx > 0) str.Append(separator);
            str.Append(x);
        }).Do();

        if (!after.IsNullOrEmpty())
            str.Append(after);

        return str;
    }

    /// <summary>
    /// Builds a composite string from the sequence of strings (convenience overload).
    /// </summary>
    /// <param name="items">Source string sequence.</param>
    /// <param name="separator">Separator inserted between elements.</param>
    /// <param name="before">Prefix appended before any elements.</param>
    /// <param name="after">Suffix appended after all elements.</param>
    /// <returns>A new <see cref="StringBuilder"/> containing the assembled text.</returns>
    /// <remarks>Fully enumerates the sequence.</remarks>
    public static StringBuilder BuildString(
        this IEnumerable<string> items,
        string separator = ", ",
        string before = "{",
        string after = "}")
    {
        return items.BuildString(new StringBuilder(), separator, before, after);
    }

    /// <summary>
    /// Determines whether the sequence is <c>null</c> or contains no elements.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="sequence">The sequence to test.</param>
    /// <returns><c>true</c> if <paramref name="sequence"/> is <c>null</c> or empty; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// If the sequence implements <see cref="ICollection{T}"/>, its Count property is consulted (O(1)).
    /// Otherwise, enumeration of at most one element occurs. For generators / single-pass enumerables,
    /// be aware this will consume the first element.
    /// </remarks>
    public static bool IsNullOrEmpty<T>(this IEnumerable<T> sequence)
    {
        if (sequence == null)
            return true;

        if (sequence is ICollection<T> collection)
            return collection.Count == 0;

        return !sequence.Any();
    }
}
