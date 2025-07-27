namespace DataFlow.Extensions;

public static class AsyncEnumerableFlatteningExtensions
{
    public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items)
    {
        await foreach (var seq in items)
        {
            await foreach (var item in seq) yield return item;
        }
    }
    public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IEnumerable<T>> items)
    {
        await foreach (var seq in items)
        {
            foreach (var item in seq) yield return item;
        }
    }
    public static async IAsyncEnumerable<T> Flatten<T>(this IEnumerable<IAsyncEnumerable<T>> items)
    {
        foreach (var seq in items)
        {
            await foreach (var item in seq) yield return item;
        }
    }
    public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, T separator)
    {
        await foreach (var seq in items)
        {
            await foreach (var item in seq) yield return item;
            yield return separator;
        }
    }
    public static async IAsyncEnumerable<T> Flatten<T>(this IAsyncEnumerable<IEnumerable<T>> items, T separator)
    {
        await foreach (var seq in items)
        {
            foreach (var item in seq) yield return item;
            yield return separator;
        }
    }
    public static async IAsyncEnumerable<T> Flatten<T>(this IEnumerable<IAsyncEnumerable<T>> items, T separator)
    {
        foreach (var seq in items)
        {
            await foreach (var item in seq) yield return item;
            yield return separator;
        }
    }

}







