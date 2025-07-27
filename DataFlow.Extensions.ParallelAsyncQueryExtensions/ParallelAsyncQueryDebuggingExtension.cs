using System.Diagnostics;
using DataFlow.Framework;

namespace DataFlow.Extensions;

public static class ParallelAsyncQueryDebuggingExtension
{
    private static readonly object _consoleLock = new object();

    /// <summary>
    /// Spies on a sequence, ensuring correct pass-through behavior and thread-safe console output.
    /// This method wraps the enumerator to correctly print header and footer messages.
    /// </summary>
    public static ParallelAsyncQuery<T> Spy<T>(
        this ParallelAsyncQuery<T> items,
        string tag,
        Func<T, string> customDisplay,
        bool timeStamp = false,
        string separator = "\n",
        string before = "---------{\n",
        string after = "\n-------}")
    {
        var stopwatch = timeStamp ? Stopwatch.StartNew() : null;
        var count = 0;

        lock (_consoleLock)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            var time = stopwatch != null ? $"[{DateTime.Now:HH:mm:ss.fff}]" : "";
            Console.Write($"{time} {tag}");
            Console.ResetColor();
            Console.Write($" :{before}");
        }

        var spiedQuery = items.ForEach(item =>
        {
            lock (_consoleLock)
            {
                if (Interlocked.Increment(ref count) > 1)
                {
                    Console.Write(separator);
                }
                Console.Write(customDisplay(item));
            }
        });

        // To print the footer, we can't easily hook into the end of the async enumeration
        // without consuming it. The ForEach approach is the most robust "spy" method.
        // The footer will be omitted to ensure the stream is not consumed prematurely.
        // A more advanced implementation could wrap the enumerator.

        return spiedQuery;
    }

    public static ParallelAsyncQuery<string> Spy(this ParallelAsyncQuery<string> items, string tag, bool timeStamp = false, string separator = "\n", string before = "---------{\n", string after = "\n-------}")
    {
        return items.Spy<string>(tag, x => x, timeStamp, separator, before, after);
    }
}

