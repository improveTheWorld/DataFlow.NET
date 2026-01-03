using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace DataFlow;

/// <summary>
/// Provides polling helpers that expose periodic, pull-based data acquisition
/// as <see cref="IAsyncEnumerable{T}"/> sequences.
/// </summary>
/// <remarks>
/// <para>
/// The methods in this class turn synchronous polling functions (either a simple
/// <see cref="Func{TResult}"/> or a <see cref="TryPollAction{T}"/> delegate following the
/// classic <c>TryGet</c> pattern) into asynchronous, cancellable push-style streams
/// (<c>IAsyncEnumerable&lt;T&gt;</c>).
/// </para>
/// <para>
/// All polling methods:
/// </para>
/// <list type="bullet">
///   <item><description>Execute the supplied delegate periodically at the specified <paramref name="pollingInterval"/>.</description></item>
///   <item><description>Evaluate a user-provided<c> stopCondition</c>(where applicable) after each poll to determine termination.</description></item>
///   <item><description>Honor the supplied <see cref="CancellationToken"/> for cooperative cancellation.</description></item>
///   <item><description>Yield elements lazily as soon as they are produced; no buffering is performed beyond the current item.</description></item>
/// </list>
/// <para>
/// <b>Thread-Safety:</b> The extensions do not introduce synchronization around the supplied delegates.
/// If the underlying <c>pollAction</c> / <c>tryPollAction</c> accesses shared mutable state, you must
/// ensure appropriate synchronization externally.
/// </para>
/// <para>
/// <b>Error Handling:</b> Any exception thrown by the user delegate or the <c>stopCondition</c>
/// will surface to the consumer on enumeration and terminate the async sequence.
/// </para>
/// <para>
/// <b>Backpressure / Rate Control:</b> The <paramref name="pollingInterval"/> provides simple
/// rate limiting. If you need adaptive or dynamic intervals, you can wrap the poll
/// function or compose a higher-level operator that adjusts the interval between iterations.
/// </para>
/// </remarks>
public static class AsyncPollingExtensions
{
    /// <summary>
    /// Creates an infinite (until cancelled) asynchronous polling sequence from a simple function
    /// that returns a value each time it is invoked.
    /// </summary>
    /// <typeparam name="T">The type of the produced items.</typeparam>
    /// <param name="pollAction">A synchronous function invoked every <paramref name="pollingInterval"/>.</param>
    /// <param name="pollingInterval">The delay between polling invocations.</param>
    /// <param name="cancellationToken">Token used to cancel the polling loop.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> that yields each non-default value returned by <paramref name="pollAction"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This overload never terminates on its own unless:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>The <paramref name="cancellationToken"/> is cancelled.</description></item>
    ///   <item><description>The consumer stops enumerating.</description></item>
    ///   <item><description>An exception is thrown by <paramref name="pollAction"/>.</description></item>
    /// </list>
    /// <para>
    /// Internally delegates to the overload that accepts a <c>stopCondition</c> using a condition that always returns false.
    /// </para>
    /// <para>
    /// Default values (i.e., <c>EqualityComparer&lt;T&gt;.Default.Equals(value, default)</c>) are skipped and not yielded.
    /// If default(T) is a legitimate value you need to observe, prefer the overload that
    /// accepts a <c>stopCondition</c> and wrap your payload in a custom struct or nullable container.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var ticks = (() => DateTime.UtcNow.Ticks)
    ///     .Poll(TimeSpan.FromMilliseconds(500), cancellationToken);
    /// await foreach (var t in ticks)
    /// {
    ///     Console.WriteLine(t);
    /// }
    /// </code>
    /// </example>
    public static IAsyncEnumerable<T> Poll<T>(
        this Func<T> pollAction,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken = default)
    {
        return pollAction.Poll(pollingInterval, (item, elapsed) => false, cancellationToken);
    }

    /// <summary>
    /// Delegate representing the classic "TryGet" pattern used for non-throwing acquisition attempts.
    /// </summary>
    /// <typeparam name="T">Type of the item produced on a successful attempt.</typeparam>
    /// <param name="item">
    /// When the method returns <c>true</c>, contains the retrieved value; otherwise contains
    /// <c>default(T)</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if an item was successfully retrieved; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This contract is used by the <see cref="Poll{T}(TryPollAction{T}, TimeSpan, Func{T, TimeSpan, bool}, CancellationToken)"/>
    /// overload to decide whether to yield a value (success) or terminate (failure or stop condition).
    /// </remarks>
    public delegate bool TryPollAction<T>(out T item);

    /// <summary>
    /// Creates an asynchronous sequence by repeatedly invoking a <see cref="TryPollAction{T}"/> delegate
    /// at a fixed interval until a stop condition is met, the delegate fails, or cancellation is requested.
    /// </summary>
    /// <typeparam name="T">Type of the items produced.</typeparam>
    /// <param name="tryPollAction">
    /// A delegate following the "Try" pattern. Returning <c>true</c> yields the item; returning <c>false</c>
    /// terminates the sequence immediately (no retry).
    /// </param>
    /// <param name="pollingInterval">The delay between consecutive polling attempts.</param>
    /// <param name="stopCondition">
    /// A predicate evaluated after each <b>successful</b> poll (i.e., after <paramref name="tryPollAction"/> returns true).
    /// It receives the yielded item and the total elapsed time since the first poll.
    /// If it returns <c>true</c>, the sequence stops (item that triggered the condition is not suppressed).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the polling loop cooperatively.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> that yields each successfully retrieved item until one of the termination criteria is met.
    /// </returns>
    /// <remarks>
    /// <para><b>Termination Conditions:</b></para>
    /// <list type="bullet">
    ///   <item><description><paramref name="tryPollAction"/> returns <c>false</c> (immediate stop, no item yielded).</description></item>
    ///   <item><description><paramref name="stopCondition"/> returns <c>true</c>.</description></item>
    ///   <item><description><paramref name="cancellationToken"/> is cancelled.</description></item>
    ///   <item><description>An exception is thrown by the action or stop condition.</description></item>
    /// </list>
    /// <para>
    /// If you want to continue polling when no item is available instead of stopping, wrap
    /// the delegate so that it returns a sentinel and still reports success, then filter later.
    /// </para>
    /// <para>
    /// The method delays for exactly <paramref name="pollingInterval"/> after each successful yield
    /// (or immediately after detecting stop condition) before the next poll unless cancellation occurs.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// AsyncPollingExtensions.TryPollAction&lt;int&gt; tryDequeue = queue.TryDequeue;
    /// await foreach (var value in tryDequeue.Poll(
    ///     TimeSpan.FromMilliseconds(100),
    ///     (item, elapsed) => elapsed &gt; TimeSpan.FromSeconds(10),
    ///     cancellationToken))
    /// {
    ///     Console.WriteLine(value);
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<T> Poll<T>(
        this TryPollAction<T> tryPollAction,
        TimeSpan pollingInterval,
        Func<T, TimeSpan, bool> stopCondition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (tryPollAction == null) throw new ArgumentNullException(nameof(tryPollAction));
        if (stopCondition == null) throw new ArgumentNullException(nameof(stopCondition));
        if (pollingInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollingInterval));

        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            var success = tryPollAction(out var item);

            if (!success)
            {
                yield break;
            }

            if (stopCondition(item, stopwatch.Elapsed))
            {
                yield return item; // Optionally: decide whether to include item; current semantics keep it.
                yield break;
            }

            yield return item;

            try
            {
                await Task.Delay(pollingInterval, cancellationToken)
                          .ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Creates an asynchronous sequence by polling a simple function (which may return default values)
    /// and yielding each non-default result until the stop condition is satisfied or cancellation occurs.
    /// </summary>
    /// <typeparam name="T">Type of the items produced.</typeparam>
    /// <param name="pollAction">Function invoked every interval to retrieve a value.</param>
    /// <param name="pollingInterval">Delay between invocations of <paramref name="pollAction"/>.</param>
    /// <param name="stopCondition">
    /// A predicate evaluated after each poll (regardless of whether the returned value is default).
    /// If it returns <c>true</c>, iteration stops (the item that triggered it is not emitted if it is default;
    /// if it is non-default it has already been yielded—see remarks).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the polling sequence.</param>
    /// <returns>An asynchronous stream of non-default items produced by <paramref name="pollAction"/>.</returns>
    /// <remarks>
    /// <para>
    /// Only non-default (<c>!EqualityComparer&lt;T&gt;.Default.Equals(item, default)</c>) items are yielded.
    /// This makes it convenient to signal "nothing new" by returning default.
    /// </para>
    /// <para>
    /// If default(T) is a legitimate payload you need to observe, wrap the value in a reference type (e.g.,
    /// <c>class Box&lt;T&gt; { public T Value; }</c>) or use <see cref="Nullable{T}"/> for value types and adjust your logic accordingly.
    /// </para>
    /// <para>
    /// The <paramref name="stopCondition"/> is evaluated AFTER the poll. If you need to prevent
    /// emitting the terminal item, you can incorporate that logic into the stop condition by
    /// checking the item first and returning <c>true</c> before it is yielded (i.e., pre-filter logic
    /// in your function).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var feed = (() => TryReadSensor()); // returns 0 when no data
    /// await foreach (var reading in feed.Poll(
    ///         TimeSpan.FromMilliseconds(250),
    ///         (value, elapsed) => elapsed &gt; TimeSpan.FromSeconds(30),
    ///         cancellationToken))
    /// {
    ///     Console.WriteLine($"Reading: {reading}");
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<T> Poll<T>(
        this Func<T> pollAction,
        TimeSpan pollingInterval,
        Func<T, TimeSpan, bool> stopCondition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (pollAction == null) throw new ArgumentNullException(nameof(pollAction));
        if (stopCondition == null) throw new ArgumentNullException(nameof(stopCondition));
        if (pollingInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollingInterval));

        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            var item = pollAction();

            if (!EqualityComparer<T>.Default.Equals(item, default))
            {
                yield return item;
            }

            if (stopCondition(item, stopwatch.Elapsed))
            {
                yield break;
            }

            try
            {
                await Task.Delay(pollingInterval, cancellationToken)
                          .ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Creates an asynchronous sequence by repeatedly invoking a <see cref="TryPollAction{T}"/> delegate
    /// at a fixed interval until the delegate first returns <c>false</c> or cancellation is requested.
    /// </summary>
    /// <typeparam name="T">Type of the items produced.</typeparam>
    /// <param name="tryPollAction">A delegate returning <c>true</c> with an item, or <c>false</c> to indicate completion.</param>
    /// <param name="pollingInterval">Delay between successful polls.</param>
    /// <param name="cancellationToken">Token used to cancel the sequence.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> yielding each successfully retrieved item until the first failure or cancellation.
    /// </returns>
    /// <remarks>
    /// Equivalent to calling the overload that accepts a <c>stopCondition</c> with a predicate that always returns false.
    /// </remarks>
    /// <example>
    /// <code>
    /// AsyncPollingExtensions.TryPollAction&lt;string&gt; tryPop = stack.TryPop;
    /// await foreach (var s in tryPop.Poll(TimeSpan.FromMilliseconds(50), token))
    /// {
    ///     Console.WriteLine(s);
    /// }
    /// </code>
    /// </example>
    public static IAsyncEnumerable<T> Poll<T>(
        this TryPollAction<T> tryPollAction,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken = default)
    {
        return tryPollAction.Poll(pollingInterval, (item, elapsed) => false, cancellationToken);
    }
}
