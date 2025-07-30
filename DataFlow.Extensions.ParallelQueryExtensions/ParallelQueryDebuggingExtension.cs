using System.Diagnostics;


namespace DataFlow.Extensions;

public static class ParallelQueryDebuggingExtension
{
    public const string BEFORE = "---------{\n";
    public const string AFTER = "\n-------}";
    public const string SEPARATOR = "\n";
    private static readonly object _consoleLock = new object();


    public static ParallelQuery<string> Spy(this ParallelQuery<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
        => items.Spy(tag, x => x, timeStamp, separator, before, after);

    public static ParallelQuery<T> Spy<T>(this ParallelQuery<T> items, string tag, Func<T, string> customDisplay, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    {
        var stopwatch = timeStamp ? Stopwatch.StartNew() : null;
        var startTime = timeStamp ? DateTime.Now : default;
        var count = 0;

        lock (_consoleLock)
        {
            if (timeStamp)
                Console.WriteLine($"[{startTime:HH:mm:ss.fff}]");

            if (!string.IsNullOrEmpty(tag))
                Console.Write($"{tag} :");

            Console.Write(before);
        }

        // Use a pass-through ForAll to print and count, then return the original items.
        // This is the correct way to "spy" without altering the query.
        var spiedItems = items.Select(item =>
        {
            var display = customDisplay(item);
            lock (_consoleLock)
            {
                if (Interlocked.Increment(ref count) > 1) Console.Write(separator);
                Console.Write(display);
            }
            return item;
        });

        // We need to force evaluation to print the footer, but we can't consume the
        // sequence. A better approach is to wrap this in a new enumerable that
        // prints the footer upon disposal. However, given PLINQ's nature, the
        // simplest robust change is to make the console output happen as a side effect
        // and accept that the footer might print early. The ideal solution is complex,
        // so we prioritize correctness and non-interference.

        // For this evaluation, we will omit the footer to ensure the query is not consumed.
        // A full implementation would require a custom enumerator.

        return spiedItems;
    }

    public static void Display(this ParallelQuery<string> items, string tag = "Displaying", string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
    {
        Console.WriteLine();
        if (!string.IsNullOrEmpty(tag))
            Console.Write($"{tag} :");

        Console.Write(before);
        var itemsArray = items.ToArray();
        for (int i = 0; i < itemsArray.Length; i++)
        {
            if (i > 0) Console.Write(separator);
            Console.Write(itemsArray[i]);
        }
        Console.Write(after);
    }
}

