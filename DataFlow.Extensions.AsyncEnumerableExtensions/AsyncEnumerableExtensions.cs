using System.Text;
using DataFlow.Extensions;
namespace DataFlow;

/// <summary>
/// Provides fundamental extension methods for <see cref="IAsyncEnumerable{T}"/> to support
/// merging, conditional termination, side-effect enumeration, string building, and basic
/// utility patterns. All methods in this class (except those returning <see cref="Task"/>)
/// are LAZY and do not execute until the returned <see cref="IAsyncEnumerable{T}"/> is
/// enumerated.
/// </summary>
/// <remarks>
/// <para>
/// These helpers complement the custom async LINQ operators defined elsewhere in the library
/// (e.g., Select, Any) and intentionally avoid forcing materialization unless explicitly
/// documented (e.g., <see cref="Do{T}(IAsyncEnumerable{T})"/>, <see cref="BuildString(System.Collections.Generic.IAsyncEnumerable{string}, StringBuilder, string, string, string)"/>).
/// </para>
/// <para>
/// Thread-safety: The extension methods themselves are stateless. Side-effect delegates
/// you supply (in <see cref="ForEach{T}(IAsyncEnumerable{T}, System.Action{T,int})"/> or
/// <see cref="ForEach{T}(IAsyncEnumerable{T}, System.Action{T})"/>) must be thread-safe
/// if the underlying source enumerator is used concurrently (rare unless you explicitly
/// parallelize enumeration).
/// </para>
/// </remarks>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Merges two already sorted asynchronous sequences into a single
    /// sorted sequence by performing a streaming two-way merge.
    /// </summary>
    /// <typeparam name="T">Element type of both input sequences.</typeparam>
    /// <param name="first">The first ordered async sequence. Must not be null.</param>
    /// <param name="second">The second ordered async sequence. Must not be null.</param>
    /// <param name="isFirstLessThanOrEqualToSecond">
    /// Comparison delegate returning <c>true</c> when the current element of
    /// <paramref name="first"/> should appear before (or at the same position as)
    /// the current element of <paramref name="second"/> in the merged ordering.
    /// </param>
    /// <returns>
    /// A merged, lazily produced, sorted sequence containing all elements from both inputs.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method assumes <paramref name="first"/> and <paramref name="second"/> are
    /// individually ordered under the same ordering implied by
    /// <paramref name="isFirstLessThanOrEqualToSecond"/>. If that assumption is violated,
    /// the merged output ordering is undefined.
    /// </para>
    /// <para>
    /// The merge is streaming: it advances each enumerator only as needed and does not
    /// buffer entire sequences in memory (O(1) extra space).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="first"/>, <paramref name="second"/>, or
    /// <paramref name="isFirstLessThanOrEqualToSecond"/> is null.
    /// </exception>
    public static async IAsyncEnumerable<T> MergeOrdered<T>(
        this IAsyncEnumerable<T> first,
        IAsyncEnumerable<T> second,
        Func<T, T, bool> isFirstLessThanOrEqualToSecond)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        if (isFirstLessThanOrEqualToSecond == null) throw new ArgumentNullException(nameof(isFirstLessThanOrEqualToSecond));

        await using var enum1 = first.GetAsyncEnumerator();
        await using var enum2 = second.GetAsyncEnumerator();

        bool hasNext1 = await enum1.MoveNextAsync();
        bool hasNext2 = await enum2.MoveNextAsync();

        while (hasNext1 && hasNext2)
        {
            if (isFirstLessThanOrEqualToSecond(enum1.Current, enum2.Current))
            {
                yield return enum1.Current;
                hasNext1 = await enum1.MoveNextAsync();
            }
            else
            {
                yield return enum2.Current;
                hasNext2 = await enum2.MoveNextAsync();
            }
        }
        // Drain remaining elements from whichever sequence still has items
        // Important: yield the current element first (it was compared but not yet yielded)
        if (hasNext1)
        {
            yield return enum1.Current;
            while (await enum1.MoveNextAsync())
                yield return enum1.Current;
        }
        else if (hasNext2)
        {
            yield return enum2.Current;
            while (await enum2.MoveNextAsync())
                yield return enum2.Current;
        }
    }


    /// <summary>
    /// Enumerates the source sequence until a supplied zero-argument stop condition
    /// returns <c>true</c>, yielding all items up to and including the last emitted
    /// item before the break.
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="items">The source async sequence.</param>
    /// <param name="stopCondition">
    /// A function evaluated after each item is yielded. When it returns <c>true</c>,
    /// enumeration stops (the item that triggered the condition is still included).
    /// </param>
    /// <returns>A lazy sequence truncated by the condition.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="stopCondition"/> is null.</exception>
    /// <remarks>
    /// The <paramref name="stopCondition"/> is evaluated after yielding each element,
    /// making the last emitted element the one for which the condition first returned <c>true</c>.
    /// </remarks>
    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<bool> stopCondition)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (stopCondition == null) throw new ArgumentNullException(nameof(stopCondition));

        await foreach (var item in items)
        {
            yield return item;
            if (stopCondition())
            {
                break;
            }
        }
    }

    /// <summary>
    /// Enumerates the sequence until the item-level stop condition returns <c>true</c>
    /// for the current element (inclusive).
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="items">The source async sequence.</param>
    /// <param name="stopCondition">
    /// A predicate evaluated after yielding each element. When it returns <c>true</c> for the
    /// element just yielded, enumeration ends.
    /// </param>
    /// <returns>A lazy sequence truncated at the first element meeting the stop condition.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="stopCondition"/> is null.</exception>
    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<T, bool> stopCondition)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (stopCondition == null) throw new ArgumentNullException(nameof(stopCondition));

        await foreach (var item in items)
        {
            yield return item;
            if (stopCondition(item))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Enumerates the sequence until the indexed stop condition returns <c>true</c>
    /// for the current element (inclusive).
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="items">The source async sequence.</param>
    /// <param name="stopCondition">
    /// A predicate receiving the current element and its zero-based index (in the truncated stream).
    /// Evaluated after yielding; if it returns <c>true</c> enumeration ends.
    /// </param>
    /// <returns>A lazy sequence truncated at the element that satisfies the predicate.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="stopCondition"/> is null.</exception>
    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<T, int, bool> stopCondition)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (stopCondition == null) throw new ArgumentNullException(nameof(stopCondition));

        int index = 0;
        await foreach (var item in items)
        {
            yield return item;
            if (stopCondition(item, index++))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Enumerates the sequence until (and including) the element whose zero-based
    /// index equals <paramref name="lastItemIdx"/>.
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="items">The source async sequence.</param>
    /// <param name="lastItemIdx">
    /// Zero-based index of the final element to yield. If negative, the result is empty.
    /// </param>
    /// <returns>A lazy subsequence of at most <c>lastItemIdx + 1</c> elements.</returns>
    public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, int lastItemIdx)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (lastItemIdx < 0) yield break;

        int index = 0;
        await foreach (var item in items)
        {
            yield return item;
            if (lastItemIdx == index++) break;
        }
    }

    /// <summary>
    /// Injects a side-effect action (with index) into a lazy pipeline without
    /// materializing the sequence. The original elements are re-emitted unchanged.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source async sequence.</param>
    /// <param name="action">
    /// Synchronous side-effect receiving the element and its zero-based index in
    /// the resulting enumeration.
    /// </param>
    /// <returns>A lazy pass-through sequence producing the same elements.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="action"/> is null.</exception>
    /// <remarks>
    /// The <paramref name="action"/> runs when enumeration reaches the element.
    /// If enumeration is partial or cancelled, some actions may never run.
    /// </remarks>
    public static IAsyncEnumerable<T> ForEach<T>(this IAsyncEnumerable<T> items, Action<T, int> action)
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
    /// Injects a side-effect action (no index) into a lazy pipeline without
    /// materializing the sequence. The original elements are re-emitted unchanged.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">Source async sequence.</param>
    /// <param name="action">Synchronous side-effect receiving each element.</param>
    /// <returns>A lazy pass-through sequence producing the same elements.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="action"/> is null.</exception>
    /// <remarks>
    /// The <paramref name="action"/> is invoked only if and when elements are enumerated.
    /// </remarks>
    public static IAsyncEnumerable<T> ForEach<T>(this IAsyncEnumerable<T> items, Action<T> action)
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
    /// Forces complete enumeration of the asynchronous sequence, discarding elements.
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="items">The async sequence to consume.</param>
    /// <returns>A task that completes when enumeration finishes or an exception is thrown.</returns>
    /// <remarks>
    /// Useful for triggering side-effects introduced by <see cref="ForEach{T}(IAsyncEnumerable{T}, System.Action{T})"/>
    /// or other lazy transformations without retaining results.
    /// </remarks>
    public static async Task Do<T>(this IAsyncEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        await foreach (var _ in items) { /* intentionally discarding */ }
    }

    /// <summary>
    /// Forces complete enumeration of the asynchronous sequence, executing an action for each element.
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="items">The async sequence to consume.</param>
    /// <param name="action">Action to execute for each element.</param>
    /// <returns>A task that completes when enumeration finishes.</returns>
    /// <remarks>
    /// <para>Eager terminal operation. Equivalent to <c>await items.ForEach(action).Do()</c>.</para>
    /// <para>Combines side-effect execution with terminal consumption in a single call.</para>
    /// </remarks>
    public static async Task Do<T>(this IAsyncEnumerable<T> items, Action<T> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));
        await foreach (var item in items) { action(item); }
    }

    /// <summary>
    /// Forces complete enumeration of the asynchronous sequence, executing an indexed action for each element.
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="items">The async sequence to consume.</param>
    /// <param name="action">Action to execute for each element, receiving the element and its zero-based index.</param>
    /// <returns>A task that completes when enumeration finishes.</returns>
    /// <remarks>
    /// <para>Eager terminal operation. Equivalent to <c>await items.ForEach(action).Do()</c>.</para>
    /// </remarks>
    public static async Task Do<T>(this IAsyncEnumerable<T> items, Action<T, int> action)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));
        int index = 0;
        await foreach (var item in items) { action(item, index++); }
    }

    /// <summary>
    /// Builds a delimited string from an async sequence of strings using a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="items">Source async sequence of string fragments.</param>
    /// <param name="str">
    /// Optional existing <see cref="StringBuilder"/> to append into. If <c>null</c>, a new instance is created.
    /// </param>
    /// <param name="separator">Separator inserted between items (except before the first).</param>
    /// <param name="before">Prefix appended before any items (if non-empty).</param>
    /// <param name="after">Suffix appended after all items (if non-empty).</param>
    /// <returns>
    /// A task producing the populated <see cref="StringBuilder"/> after the entire sequence is enumerated.
    /// </returns>
    /// <remarks>
    /// This method materializes the entire sequence. Do not use on infinite or very large streams
    /// unless you intentionally want to accumulate all data.
    /// </remarks>
    /// <exception cref="ArgumentNullException">If <paramref name="items"/> is null.</exception>
    public static async Task<StringBuilder> BuildString(
        this IAsyncEnumerable<string> items,
        StringBuilder? str = null,
        string separator = ", ",
        string before = "{",
        string after = "}")
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        str ??= new StringBuilder();

        if (!before.IsNullOrEmpty())
            str.Append(before);

        await items
            .ForEach((x, idx) =>
            {
                if (idx > 0) str.Append(separator);
                str.Append(x);
            })
            .Do();

        if (!after.IsNullOrEmpty())
            str.Append(after);

        return str;
    }

    /// <summary>
    /// Convenience overload that builds a delimited string using a newly allocated
    /// <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="items">Source async sequence of string fragments.</param>
    /// <param name="separator">Separator inserted between items.</param>
    /// <param name="before">Prefix appended before any items.</param>
    /// <param name="after">Suffix appended after all items.</param>
    /// <returns>A task producing the populated <see cref="StringBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="items"/> is null.</exception>
    /// <inheritdoc cref="BuildString(System.Collections.Generic.IAsyncEnumerable{string}, StringBuilder, string, string, string)"/>
    public static async Task<StringBuilder> BuildString(
        this IAsyncEnumerable<string> items,
        string separator = ", ",
        string before = "{",
        string after = "}")
    {
        return await items.BuildString(new StringBuilder(), separator, before, after);
    }

    /// <summary>
    /// Determines whether an async sequence is <c>null</c> or contains no elements.
    /// </summary>
    /// <typeparam name="T">Element type of the sequence.</typeparam>
    /// <param name="sequence">The sequence to test.</param>
    /// <returns>
    /// A task that resolves to <c>true</c> if <paramref name="sequence"/> is <c>null</c>
    /// or empty; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Enumeration stops as soon as the first element is observed. If the sequence
    /// has expensive side-effects on first enumeration, be aware this method triggers them.
    /// </remarks>
    public static async Task<bool> IsNullOrEmpty<T>(this IAsyncEnumerable<T> sequence)
    {
        if (sequence == null) return true;
        return !await sequence.Any();
    }
}
