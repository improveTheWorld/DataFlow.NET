using System.Runtime.CompilerServices;
using DataFlow.Framework;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides extension methods that adapt or control the emission characteristics
    /// of <see cref="IAsyncEnumerable{T}"/> sources within the DataFlow framework.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These helpers enable:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>Creating a named <see cref="IDataSource{T}"/> from any <see cref="IAsyncEnumerable{T}"/>
    ///     so it can participate in a <see cref="UnifiedStream{T}"/> multi-source aggregation.</description>
    ///   </item>
    ///   <item>
    ///     <description>Throttling emission rate to introduce a minimum delay between items (useful for
    ///     demo scenarios, UI updates, load shaping, or avoiding overwhelming downstream consumers).</description>
    ///   </item>
    /// </list>
    /// <para>
    /// All throttling methods are <b>lazy</b>: the underlying sequence is not iterated until the returned
    /// <see cref="IAsyncEnumerable{T}"/> itself is enumerated. Cancellation is cooperative via the provided
    /// <see cref="CancellationToken"/>.
    /// </para>
    /// </remarks>
    public static class AsyncEnumerableDataSourceExtensions
    {
        /// <summary>
        /// Throttles an asynchronous sequence so that each element is yielded only after waiting
        /// the specified <paramref name="interval"/> following the previous emission.
        /// </summary>
        /// <typeparam name="T">The element type of the source sequence.</typeparam>
        /// <param name="source">The source asynchronous sequence to throttle.</param>
        /// <param name="interval">The fixed time delay inserted after each yielded element.</param>
        /// <param name="cancellationToken">
        /// A token that cancels enumeration. If cancellation is requested while awaiting the delay,
        /// the sequence stops without throwing unless the underlying source throws on cancellation.
        /// </param>
        /// <returns>
        /// A lazy <see cref="IAsyncEnumerable{T}"/> that yields each original element in order, inserting
        /// a delay of <paramref name="interval"/> between emissions.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Delay semantics: The method yields the source element immediately after receiving it, then
        /// waits the specified interval before requesting the next element. Thus, the first element
        /// is not delayed; subsequent elements are spaced by approximately the interval duration
        /// (subject to scheduler timing and upstream latency).
        /// </para>
        /// <para>
        /// Cancellation: If <paramref name="cancellationToken"/> is triggered during the delay, iteration
        /// ends gracefully. Any already yielded items remain valid. No further items are requested.
        /// </para>
        /// <para>
        /// Exceptions: Exceptions thrown by the source propagate unchanged. A <see cref="TaskCanceledException"/>
        /// during the internal delay is swallowed to end iteration cleanly.
        /// </para>
        /// <para>
        /// Thread-safety: This operator does not add synchronization; enumeration should remain single-consumer.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// await foreach (var reading in sensorStream
        ///     .Throttle(TimeSpan.FromMilliseconds(200), cancellationToken))
        /// {
        ///     UpdateDashboard(reading);
        /// }
        /// </code>
        /// </example>
        public static async IAsyncEnumerable<T> Throttle<T>(
            this IAsyncEnumerable<T> source,
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                yield return item;
                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Graceful termination on cancellation during delay
                    break;
                }
            }
        }

        /// <summary>
        /// Throttles an asynchronous sequence using a delay specified in milliseconds
        /// between yielded elements.
        /// </summary>
        /// <typeparam name="T">The element type of the source sequence.</typeparam>
        /// <param name="source">The source asynchronous sequence to throttle.</param>
        /// <param name="intervalInMs">Delay in milliseconds applied after yielding each element (except the first).</param>
        /// <param name="cancellationToken">
        /// A token that cancels enumeration. Cancellation during the delay stops iteration without throwing.
        /// </param>
        /// <returns>
        /// A lazy <see cref="IAsyncEnumerable{T}"/> that yields each source element and enforces at least
        /// <paramref name="intervalInMs"/> milliseconds of spacing after each emission.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This overload is a convenience wrapper around the <see cref="Throttle{T}(IAsyncEnumerable{T}, TimeSpan, CancellationToken)"/>
        /// method. Negative values are not validated here; passing a negative number results in a zero or near-zero delay
        /// per <see cref="Task.Delay(TimeSpan, CancellationToken)"/> behavior.
        /// </para>
        /// <inheritdoc cref="Throttle{T}(IAsyncEnumerable{T}, TimeSpan, CancellationToken)" select="remarks"/>
        /// </remarks>
        public static async IAsyncEnumerable<T> Throttle<T>(
            this IAsyncEnumerable<T> source,
            double intervalInMs,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var interval = TimeSpan.FromMilliseconds(intervalInMs);
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                yield return item;
                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        // Convenience unifier for two sources into an AsyncEnumerable<T>
        public static UnifiedStream<T> Unify<T>(
            this (IAsyncEnumerable<T> source, string name) source1,
            IAsyncEnumerable<T> source2,
            string name2,
            UnifyErrorMode errorMode = UnifyErrorMode.FailFast,
            UnifyFairness fairness = UnifyFairness.RoundRobin)
        {
            return new UnifiedStream<T>(new UnifyOptions { Fairness = fairness, ErrorMode = errorMode })
                .Unify(source1.source, source1.name)
                .Unify(source2, name2);
        }

        // Add a named source to an existing AsyncEnumerable<T>
        public static UnifiedStream<T> Unify<T>(
            this (IAsyncEnumerable<T> source, string name) source1,
            UnifiedStream<T> target)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            return target.Unify(source1.source, source1.name);
        }

    }
}