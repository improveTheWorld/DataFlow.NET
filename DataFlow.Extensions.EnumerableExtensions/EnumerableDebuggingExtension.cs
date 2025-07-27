using System.Diagnostics;

namespace DataFlow.Extensions;

public static class EnumerableDebuggingExtension
{

    public const string BEFORE = "---------{\n";
    public const string AFTER = "\n-------}";
    public const string SEPARATOR = "\n";
    public static IEnumerable<string> Spy(this IEnumerable<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    => items.Spy<string>(tag, x => x, timeStamp, separator, before, after);

    public static IEnumerable<T> Spy<T>(this IEnumerable<T> items, string tag, Func<T, string> customDispay, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    {
        string startedAt = string.Empty;
        Stopwatch stopwatch = new();
        if (timeStamp)
        {
            DateTime now = DateTime.Now;
            startedAt = $"[{now.Hour}:{now.Minute}:{now.Second}.{now.Millisecond}]";
            stopwatch = new Stopwatch();

            // Start the stopwatch
            stopwatch.Start();
        }

        Console.WriteLine(startedAt);
        if (!tag.IsNullOrEmpty())
            Console.Write(tag); Console.Write(" :");

        Console.Write(before);
        int i = 0;
        foreach (var item in items)
        {
            if (i != 0) Console.Write(separator);
            Console.Write(customDispay(item));
            yield return item;

            i++;
        }

        Console.Write(after);
        if (timeStamp)
        {
            // Stop the stopwatch
            stopwatch.Stop();
            Console.Write($"[{stopwatch.Elapsed.TotalMilliseconds} ms]");
        }

    }

    public static void Display(this IEnumerable<string> items, string tag = "Displaying", string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    {
        Console.WriteLine();
        if (!tag.IsNullOrEmpty())
            Console.Write(tag); Console.Write(" :");

        Console.Write(before);
        int i = 0;
        foreach (var item in items)
        {
            if (i != 0) Console.Write(separator);
            Console.Write(item);
            i++;
        }
        Console.Write(after);
    }
}



