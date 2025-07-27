using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace DataFlow.Extensions;

public static class AsyncPollingExtensions
{
    public static IAsyncEnumerable<T> Poll<T>(
  this Func<T> pollAction,
  TimeSpan pollingInterval,
  CancellationToken cancellationToken = default)
    {
        // Call the main overload with a stop condition that never triggers.
        return pollAction.Poll(pollingInterval, (item, elapsed) => false, cancellationToken);
    }
    /// <summary>
    /// Represents a method that attempts to retrieve an item.
    /// This is the standard "TryGet" pattern.
    /// </summary>
    /// <typeparam name="T">The type of the item to retrieve.</typeparam>
    /// <param name="item">When this method returns, contains the retrieved item if the
    /// retrieval succeeded, or the default value for T if it failed.</param>
    /// <returns>true if an item was successfully retrieved; otherwise, false.</returns>
    public delegate bool TryPollAction<T>(out T item);

    /// <summary>
    /// Creates an IAsyncEnumerable<T> by polling a function that uses the "TryGet" pattern.
    /// </summary>
    /// <typeparam name="T">The type of item to poll for.</typeparam>
    /// <param name="tryPollAction">
    /// The source function to be called periodically, following the TryGet pattern.
    /// </param>
    /// <param name="pollingInterval">
    /// The time to wait between calls to the tryPollAction.
    /// </param>
    /// <param name="stopCondition">x²x²x²x 
    /// A function evaluated after each poll. It receives the success status, the polled 
    /// item (which may be default), and the total elapsed time. Polling stops when it 
    /// returns true.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token to stop the polling process externally.
    /// </param>
    /// <returns>
    /// An IAsyncEnumerable<T> that yields items as they are discovered by the tryPollAction.
    /// </returns>
    public static async IAsyncEnumerable<T> Poll<T>(
        this TryPollAction<T> tryPollAction,
        TimeSpan pollingInterval,
        Func<T, TimeSpan, bool> stopCondition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            // 1. Poll for the next element using the TryGet pattern
            bool success = tryPollAction(out T item);

            // 2. Check the master stop condition
            if (!success || stopCondition(item, stopwatch.Elapsed))
            {
                yield break;
            }

            // 3. If the poll was successful, yield the item.
            //    No need for a default check; the 'success' bool is the source of truth.
            else
            {
                yield return item;
            }

            // 4. Wait for the specified polling period
            try
            {
                await Task.Delay(pollingInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

    }

    /// <summary>
    /// Creates an IAsyncEnumerable<T> by treating the source function as a polling action.
    /// </summary>
    /// <remarks>
    /// This creates a fluent API allowing you to write: myPollFunc.Poll(...)
    /// </remarks>
    /// <typeparam name="T">The type of item to poll for.</typeparam>
    /// <param name="pollAction">
    /// The source function to be called periodically. It will be extended by this method.
    /// </param>
    /// <param name="pollingInterval">
    /// The time to wait between calls to the pollAction.
    /// </param>
    /// <param name="stopCondition">
    /// A function that is evaluated after each poll. Polling stops when it returns true.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token to stop the polling process externally.
    /// </param>
    /// <returns>
    /// An IAsyncEnumerable<T> that yields items as they are discovered by the pollAction.
    /// </returns>
    public static async IAsyncEnumerable<T> Poll<T>(
        this Func<T> pollAction,
        TimeSpan pollingInterval,
        Func<T, TimeSpan, bool> stopCondition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            T item = pollAction();

            if (stopCondition(item, stopwatch.Elapsed))
            {
                yield break;
            }

            if (!EqualityComparer<T>.Default.Equals(item, default(T)))
            {
                yield return item;
            }

            try
            {
                await Task.Delay(pollingInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Creates an IAsyncEnumerable<T> by polling a "TryGet" function indefinitely until cancelled or no more elements.
    /// </summary>
    public static IAsyncEnumerable<T> Poll<T>(
        this TryPollAction<T> tryPollAction,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken = default)
    {
        // Call the main overload with a stop condition that never triggers.
        return tryPollAction.Poll(pollingInterval, (item, elapsed) => false, cancellationToken);
    }

}







