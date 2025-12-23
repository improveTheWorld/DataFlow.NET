using System.Collections.Concurrent;
using System.Text;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides utility and helper extension methods for working with <see cref="ParallelQuery{TSource}"/> sequences.
    /// </summary>
    /// <remarks>
    /// All methods in this class extend PLINQ (<see cref="ParallelQuery{TSource}"/>) pipelines.
    /// <para>
    /// Unless explicitly stated otherwise, methods returning <see cref="ParallelQuery{T}"/> are
    /// lazy/deferred and do not trigger execution until a terminal operator (e.g. <c>.ToArray()</c>, <c>.ForAll()</c>,
    /// <c>.Sum()</c>, etc.) is invoked.
    /// </para>
    /// <para>
    /// Methods that return <see cref="void"/> (e.g. <see cref="Do{T}(ParallelQuery{T})"/>,
    /// <see cref="Do{T}(ParallelQuery{T}, Action)"/>) are terminal and force enumeration.
    /// </para>
    /// <para>
    /// Thread-safety: The provided delegates (<see cref="Action{T}"/>, <see cref="Func{T,TResult}"/>) may be
    /// executed concurrently on multiple threads. Any captured state must be thread-safe.
    /// </para>
    /// </remarks>
    public static class ParallelQueryExtensions
    {
        /// <summary>
        /// Returns a contiguous slice of a parallel sequence starting at a specified index.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="sequence">The source parallel sequence.</param>
        /// <param name="start">Zero-based index at which to begin returning elements.</param>
        /// <param name="count">Number of elements to return.</param>
        /// <returns>A new <see cref="ParallelQuery{T}"/> containing the specified slice.</returns>
        /// <remarks>
        /// This is a convenience wrapper that composes <see cref="ParallelEnumerable.Skip{TSource}(ParallelQuery{TSource}, int)"/>
        /// and <see cref="ParallelEnumerable.Take{TSource}(ParallelQuery{TSource}, int)"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="start"/> or <paramref name="count"/> is negative.</exception>
        public static ParallelQuery<T> Take<T>(this ParallelQuery<T> sequence, int start, int count)
        {
            if (sequence is null) throw new ArgumentNullException(nameof(sequence));
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            return sequence.Skip(start).Take(count);
        }

        /// <summary>
        /// Applies an indexed side-effecting action to each element while preserving the query shape (pass-through).
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="items">The source parallel sequence.</param>
        /// <param name="action">
        /// Action receiving the element and its zero-based index. Index values are assigned in the natural
        /// PLINQ partitioned enumeration order (not guaranteed to be sequential across threads).
        /// </param>
        /// <returns>
        /// A new <see cref="ParallelQuery{T}"/> that yields the original elements after performing the side effect.
        /// </returns>
        /// <remarks>
        /// Because PLINQ processes partitions independently, the index passed to <paramref name="action"/> is
        /// the projection index from the local partition ordering and not a deterministic global order.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="action"/> is <c>null</c>.</exception>
        public static ParallelQuery<T> ForEach<T>(this ParallelQuery<T> items, Action<T, int> action)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (action is null) throw new ArgumentNullException(nameof(action));

            return items.Select((x, idx) =>
            {
                action(x, idx);
                return x;
            });
        }

        /// <summary>
        /// Applies a side-effecting action to each element while preserving the query shape (pass-through).
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="items">The source parallel sequence.</param>
        /// <param name="action">Action to execute per element (may run concurrently).</param>
        /// <returns>The original elements after side-effects have been scheduled in the pipeline.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="action"/> is <c>null</c>.</exception>
        public static ParallelQuery<T> ForEach<T>(this ParallelQuery<T> items, Action<T> action)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (action is null) throw new ArgumentNullException(nameof(action));

            return items.Select(x =>
            {
                action(x);
                return x;
            });
        }


        /// <summary>
        /// Forces enumeration of the parallel sequence and discards all elements.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">The source sequence.</param>
        /// <remarks>
        /// Terminal operation. Equivalent to invoking <c>items.ForAll(_ =&gt; { })</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
        public static void Do<T>(this ParallelQuery<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            items.ForAll(_ => { });
        }

        /// <summary>
        /// Builds a composite string from a parallel sequence of strings using a <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="items">The source parallel sequence of strings.</param>
        /// <param name="str">Optional existing <see cref="StringBuilder"/> to append into (created if <c>null</c>).</param>
        /// <param name="separator">Separator inserted between elements (ignored for first element).</param>
        /// <param name="before">Prefix appended before the first element (if not empty).</param>
        /// <param name="after">Suffix appended after the last element (if not empty).</param>
        /// <returns>The populated <see cref="StringBuilder"/> instance.</returns>
        /// <remarks>
        /// Materializes the entire parallel sequence into an array (ordering determined by PLINQ's current merge strategy)
        /// then appends sequentially. If a deterministic order is required, ensure an <c>OrderBy</c> clause exists prior
        /// or call <c>.AsSequential()</c> before invoking.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
        public static StringBuilder BuildString(
            this ParallelQuery<string> items,
            StringBuilder? str = null,
            string separator = ", ",
            string before = "{",
            string after = "}")
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            str ??= new StringBuilder();

            if (!string.IsNullOrEmpty(before))
                str.Append(before);

            var itemsArray = items.ToArray(); // Materialize first
            for (int i = 0; i < itemsArray.Length; i++)
            {
                if (i > 0) str.Append(separator);
                str.Append(itemsArray[i]);
            }

            if (!string.IsNullOrEmpty(after))
                str.Append(after);

            return str;
        }

        /// <summary>
        /// Builds a composite string from a parallel sequence of strings using a newly allocated <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="items">The source parallel sequence of strings.</param>
        /// <param name="separator">Separator inserted between elements.</param>
        /// <param name="before">Prefix appended before the first element (if not empty).</param>
        /// <param name="after">Suffix appended after the last element (if not empty).</param>
        /// <returns>A new <see cref="StringBuilder"/> containing the concatenated text.</returns>
        /// <remarks>See <see cref="BuildString(ParallelQuery{string}, StringBuilder?, string, string, string)"/> for details.</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
        public static StringBuilder BuildString(
            this ParallelQuery<string> items,
            string separator = ", ",
            string before = "{",
            string after = "}")
        {
            return items.BuildString(new StringBuilder(), separator, before, after);
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="int"/> values in a thread-safe manner using a 64-bit accumulator.
        /// </summary>
        /// <param name="source">The parallel source of integers.</param>
        /// <returns>The 32-bit integer sum of all elements.</returns>
        /// <remarks>
        /// Accumulates in a <see cref="long"/> to reduce overflow risk during addition. After aggregation,
        /// the result is range-checked and cast to <see cref="int"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        /// <exception cref="OverflowException">
        /// Thrown if the accumulated sum is outside <see cref="int.MinValue"/>.. <see cref="int.MaxValue"/>.
        /// </exception>
        public static int Sum(this ParallelQuery<int> source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            long sum = 0;
            // ForAll is a terminal operation in PLINQ, suitable for side-effects like this.
            source.ForAll(item => Interlocked.Add(ref sum, item));

            if (sum > int.MaxValue || sum < int.MinValue)
            {
                throw new OverflowException("The sum of the sequence is outside the bounds of a 32-bit integer.");
            }

            return (int)sum;
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="long"/> values in a thread-safe manner.
        /// </summary>
        /// <param name="source">The parallel source of long values.</param>
        /// <returns>The 64-bit sum.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        public static long Sum(this ParallelQuery<long> source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            long sum = 0;
            source.ForAll(item => Interlocked.Add(ref sum, item));
            return sum;
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="float"/> values in a thread-safe manner.
        /// </summary>
        /// <param name="source">The parallel source of float values.</param>
        /// <returns>The single-precision sum.</returns>
        /// <remarks>
        /// Uses a <c>lock</c> to guard accumulation because <see cref="Interlocked"/> does not support floating-point atomic adds.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        public static float Sum(this ParallelQuery<float> source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            float sum = 0;
            object lockObj = new object();
            source.ForAll(item =>
            {
                lock (lockObj)
                {
                    sum += item;
                }
            });
            return sum;
        }

        /// <summary>
        /// Computes the sum of a sequence of <see cref="decimal"/> values in a thread-safe manner.
        /// </summary>
        /// <param name="source">The parallel source of decimal values.</param>
        /// <returns>The decimal sum.</returns>
        /// <remarks>
        /// Uses a <c>lock</c> for thread safety because <see cref="Interlocked"/> does not support decimal operations.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        public static decimal Sum(this ParallelQuery<decimal> source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            decimal sum = 0;
            object lockObj = new object();
            source.ForAll(item =>
            {
                lock (lockObj)
                {
                    sum += item;
                }
            });
            return sum;
        }

        /// <summary>
        /// Determines whether a parallel sequence is <c>null</c> or contains no elements.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="sequence">The source sequence. May be <c>null</c>.</param>
        /// <returns><c>true</c> if <paramref name="sequence"/> is <c>null</c> or empty; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// If non-null, this method enumerates at most one element to determine emptiness.
        /// </remarks>
        public static bool IsNullOrEmpty<T>(this ParallelQuery<T>? sequence)
        {
            if (sequence == null) return true;
            return !sequence.Any();
        }
    }
}