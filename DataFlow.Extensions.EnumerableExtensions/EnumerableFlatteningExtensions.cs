namespace DataFlow.Extensions;

public static class EnumerableFlatteningExtensions
{
    public static  IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items)
    {
        foreach (IEnumerable<T> seq in items)
        {
            foreach (var item in seq) yield return item;
        }
    }

    public static  IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items, T separator)
    {
        foreach (IEnumerable<T> seq in items)
        {
            foreach (var item in seq) yield return item;
            yield return separator;
        }
    }
}


