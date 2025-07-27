using System.Runtime.CompilerServices;
using DataFlow.Framework;


namespace DataFlow.Extensions;

public static class AsyncEnumerableDataSourceExtensions
{

    /// <summary>
    /// Throttles an asynchronous sequence, emitting items at a specified interval.
    /// </summary>
    /// <returns>An IAsyncEnumerable that yields items from the source sequence with a delay between each item.</returns>
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
                break;
            }
        }
    }

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

    public static IDataSource<T> ToDataSource<T>(this IAsyncEnumerable<T> sourceEnumerable, string name)
    {
        return new AsyncEnumDataSource<T>(sourceEnumerable, name);
    }

}







