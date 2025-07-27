using System.Diagnostics;


namespace DataFlow.Extensions;

public static class AsyncEnumerableDebuggingExtensions
{



    public const string BEFORE = "---------{\n";
    public const string AFTER = "\n-------}";
    public const string SEPARATOR = "\n";
    public static IAsyncEnumerable<string> Spy(this IAsyncEnumerable<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    {
        return items.Spy<string>(tag, x => x, timeStamp, separator, before, after);
    }

    public static async IAsyncEnumerable<T> Spy<T>(
    this IAsyncEnumerable<T> items,
    string tag,
    Func<T, string> customDisplay,
    bool timeStamp = false,
    string separator = "\n",
    string before = "---------{\n",
    string after = "\n-------}")
    {
        string startedAt = string.Empty;
        Stopwatch stopwatch = new();

        if (timeStamp)
        {
            DateTime now = DateTime.Now;
            startedAt = $"[{now:HH:mm:ss.fff}]";
            stopwatch = Stopwatch.StartNew();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{startedAt} {tag}");
        Console.ResetColor();
        Console.Write($" :{before}");

        int count = 0;
        await foreach (var item in items)
        {
            if (count > 0) Console.Write(separator);
            Console.Write(customDisplay(item));
            yield return item;
            count++;
        }

        Console.Write(after);

        if (timeStamp)
        {
            stopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($" [{stopwatch.Elapsed.TotalMilliseconds:F0}ms, {count} items]");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    public static async Task Display(this IAsyncEnumerable<string?> items,
    string tag = "Displaying", string separator = SEPARATOR,
    string before = BEFORE, string after = AFTER)
    {
        Console.WriteLine();
        if (!tag.IsNullOrEmpty())
            Console.Write(tag); Console.Write(" :");

        Console.Write(before);
        int i = 0;
        await foreach (var item in items)
        {
            if (i != 0) Console.Write(separator);
            Console.Write($"{i} :  {item}");
            i++;
        }
        Console.Write(after);
    }




    //------------------------------------------- SELECT MANY

    /// <summary>
    /// Projects each element to an IAsyncEnumerable and flattens the resulting sequences.
    /// </summary>
    public static async IAsyncEnumerable<TResult> SelectMany<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, IAsyncEnumerable<TResult>> selector)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));

        await foreach (var item in source)
        {
            await foreach (var subItem in selector(item))
            {
                yield return subItem;
            }
        }
    }

    /// <summary>
    /// SelectMany with result selector
    /// </summary>
    public static async IAsyncEnumerable<TResult> SelectMany<T, TCollection, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, IAsyncEnumerable<TCollection>> collectionSelector,
        Func<T, TCollection, TResult> resultSelector)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (collectionSelector == null)
            throw new ArgumentNullException(nameof(collectionSelector));
        if (resultSelector == null)
            throw new ArgumentNullException(nameof(resultSelector));

        await foreach (var item in source)
        {
            await foreach (var subItem in collectionSelector(item))
            {
                yield return resultSelector(item, subItem);
            }
        }
    }

    //------------------------------------------- DISTINCT

    /// <summary>
    /// Returns distinct elements from a sequence.
    /// </summary>
    public static async IAsyncEnumerable<T> Distinct<T>(
        this IAsyncEnumerable<T> source,
        IEqualityComparer<T>? comparer = null)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var seen = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);

        await foreach (var item in source)
        {
            if (seen.Add(item))
                yield return item;
        }
    }

    //------------------------------------------- CONCAT

    /// <summary>
    /// Concatenates two sequences.
    /// </summary>
    public static async IAsyncEnumerable<T> Concat<T>(
        this IAsyncEnumerable<T> first,
        IAsyncEnumerable<T> second)
    {
        if (first == null)
            throw new ArgumentNullException(nameof(first));
        if (second == null)
            throw new ArgumentNullException(nameof(second));

        await foreach (var item in first)
        {
            yield return item;
        }

        await foreach (var item in second)
        {
            yield return item;
        }
    }

    //------------------------------------------- APPEND / PREPEND

    /// <summary>
    /// Appends a value to the end of the sequence.
    /// </summary>
    public static async IAsyncEnumerable<T> Append<T>(
        this IAsyncEnumerable<T> source,
        T element)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        await foreach (var item in source)
        {
            yield return item;
        }

        yield return element;
    }

    /// <summary>
    /// Prepends a value to the beginning of the sequence.
    /// </summary>
    public static async IAsyncEnumerable<T> Prepend<T>(
        this IAsyncEnumerable<T> source,
        T element)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        yield return element;

        await foreach (var item in source)
        {
            yield return item;
        }
    }




    //------------------------------------------- AGGREGATE

    /// <summary>
    /// Applies an accumulator function over a sequence.
    /// </summary>
    public static async Task<T> Aggregate<T>(this IAsyncEnumerable<T> source, Func<T, T, T> func)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        await using var enumerator = source.GetAsyncEnumerator();

        if (!await enumerator.MoveNextAsync())
            throw new InvalidOperationException("Sequence contains no elements");

        T result = enumerator.Current;
        while (await enumerator.MoveNextAsync())
        {
            result = func(result, enumerator.Current);
        }

        return result;
    }





}







