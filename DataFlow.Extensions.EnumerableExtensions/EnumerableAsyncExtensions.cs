using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DataFlow;

public static class EnumerableAsyncExtensions
{

 

   
    
    /// <summary>
    /// Wraps a synchronous IEnumerable<T> in a cooperative IAsyncEnumerable<T>.
    /// It processes items in batches, yielding control periodically to prevent
    /// blocking the thread for extended periods.
    /// </summary>
    /// <param name="items">The source synchronous enumerable.</param>
    /// <param name="yieldThresholdMs">
    /// The time slice in milliseconds. After this much time has elapsed in a 
    /// synchronous batch, the method will yield control. Defaults to 15ms, which
    /// is ideal for maintaining UI responsiveness (under a 60fps frame budget).
    /// Set to long.MaxValue to effectively disable yielding and maximize throughput.
    /// </param>
    public static async IAsyncEnumerable<T> Async<T>(
        this IEnumerable<T> items,
        long yieldThresholdMs = 15,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) // Parameter with default value
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        // A threshold of 0 or less would yield on every item, which is inefficient.
        // We can treat it as a request to be highly responsive.
        if (yieldThresholdMs <= 0) yieldThresholdMs = 1;

        var stopwatch = Stopwatch.StartNew();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;

            if (stopwatch.ElapsedMilliseconds > yieldThresholdMs)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                stopwatch.Restart();
            }
        }
    }


    // Cooperative yielding helper for IEnumerable<T>
    // This is NOT a backpressure mechanism; it just yields control periodically.
    public static async IAsyncEnumerable<T> BufferAsync<T>(
        this IEnumerable<T> source,
        long yieldThresholdMs = 15,
        bool runOnBackgroundThread = false,
        BoundedChannelOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (yieldThresholdMs <= 0) yieldThresholdMs = 1;

        if (!runOnBackgroundThread)
        {
            var sw = Stopwatch.StartNew();
            foreach (var item in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;

                if (sw.ElapsedMilliseconds > yieldThresholdMs)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                    sw.Restart();
                }
            }
            yield break;
        }

        // Channel-based offload for blocking IEnumerable producers (with backpressure)
        options ??= new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = true
        };
        var channel = Channel.CreateBounded<T>(options);

        _ = Task.Run(() =>
        {
            try
            {
                foreach (var item in source)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    channel.Writer.WriteAsync(item, cancellationToken).AsTask().Wait(cancellationToken);
                }
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    // Channel-based buffering/backpressure for IAsyncEnumerable<T>
    // Use this to decouple producers and consumers with explicit capacity and policy.
    public static async IAsyncEnumerable<T> WithBoundedBuffer<T>(
        this IAsyncEnumerable<T> source,
        BoundedChannelOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        options ??= new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = true
        };

        var channel = Channel.CreateBounded<T>(options);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                {
                    await channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
                }
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        await foreach (var item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    // Convenience overload: capacity + FullMode
    public static IAsyncEnumerable<T> WithBoundedBuffer<T>(
        this IAsyncEnumerable<T> source,
        int capacity,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
        CancellationToken ct = default)
    {
        var cap = Math.Max(1, capacity);
        var opts = new BoundedChannelOptions(cap)
        {
            FullMode = fullMode,
            SingleWriter = true,
            SingleReader = true
        };
        return WithBoundedBuffer(source, opts, ct);
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


