using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DataFlow.Extensions;

public static class EnumerableAsyncExtensions
{
    /// <summary>
    /// Wraps a synchronous IEnumerable<T> in a cooperative IAsyncEnumerable<T>.
    /// It processes items in batches, yielding control periodically to prevent
    /// blocking the thread for extended periods.
    /// </summary>
    /// <param name="source">The source synchronous enumerable.</param>
    /// <param name="yieldThresholdMs">
    /// The time slice in milliseconds. After this much time has elapsed in a 
    /// synchronous batch, the method will yield control. Defaults to 15ms, which
    /// is ideal for maintaining UI responsiveness (under a 60fps frame budget).
    /// Set to long.MaxValue to effectively disable yielding and maximize throughput.
    /// </param>
    public static async IAsyncEnumerable<T> Async<T>(
        this IEnumerable<T> source,
        long yieldThresholdMs = 15) // Parameter with default value
    {
        // A threshold of 0 or less would yield on every item, which is inefficient.
        // We can treat it as a request to be highly responsive.
        if (yieldThresholdMs <= 0) yieldThresholdMs = 1;

        var stopwatch = Stopwatch.StartNew();

        foreach (var item in source)
        {
            yield return item;

            if (stopwatch.ElapsedMilliseconds > yieldThresholdMs)
            {
                await Task.Yield();
                stopwatch.Restart();
            }
        }
    }


    /// <summary>
    /// Throttles a synchronous sequence, converting it to an asynchronous one that emits items at a specified interval.
    /// </summary>
    /// <returns>An IAsyncEnumerable that yields items from the source sequence with a delay between each item.</returns>
    public static async IAsyncEnumerable<T> Throttle<T>(
        this IEnumerable<T> source,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

    public static async IAsyncEnumerable<T> Throttle<T>(
        this IEnumerable<T> source,
        double intervalInMs,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var interval = TimeSpan.FromMilliseconds(intervalInMs);
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
}


