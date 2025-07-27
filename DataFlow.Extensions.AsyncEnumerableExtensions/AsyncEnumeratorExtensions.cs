namespace DataFlow.Extensions;

public static class AsyncEnumeratorExtensions
{
    public static async Task<(bool, T?)> TryGetNext<T>(this IAsyncEnumerator<T> enumerator)
    {
        if (await enumerator.MoveNextAsync())
        {
            return (true, enumerator.Current);
        }
        return (false, default(T));
    }
    public static async Task<T?> GetNext<T>(this IAsyncEnumerator<T> enumerator)
    {
        if (await enumerator.MoveNextAsync())
        {
            return enumerator.Current;

        }
        else

            return default(T);
    }
}







