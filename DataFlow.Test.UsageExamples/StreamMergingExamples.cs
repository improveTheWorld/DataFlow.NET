using DataFlow.Extensions;
using DataFlow.Framework;

namespace DataFlow.App.UsageExamples;

/// <summary>
/// Examples from Stream-Merging.md - AsyncEnumerableMerger for multi-source streams
/// </summary>
public static class StreamMergingExamples
{
    public record LogEntry(DateTime Timestamp, string Level, string Message, string Source);

    /// <summary>
    /// Stream-Merging: Basic multi-source unification
    /// </summary>
    public static async Task BasicStreamMergingAsync()
    {
        Console.WriteLine("=== Basic Stream Merging ===\n");

        // Simulate multiple log sources
        var webServerLogs = SimulateLogSource("WebServer", 3);
        var databaseLogs = SimulateLogSource("Database", 2);
        var authLogs = SimulateLogSource("AuthService", 2);

        Console.WriteLine("Merging logs from 3 sources...\n");

        // Merge all sources into a single stream
        var merger = new UnifiedStream<LogEntry>()
            .Unify(webServerLogs, "web")
            .Unify(databaseLogs, "db")
            .Unify(authLogs, "auth");

        await foreach (var log in merger)
        {
            Console.WriteLine($"  [{log.Source}] {log.Level}: {log.Message}");
        }

        Console.WriteLine("\n✅ All sources unified into single stream");
    }

    /// <summary>
    /// Stream-Merging: Merging with filtering predicate
    /// </summary>
    public static async Task StreamMergingWithFilterAsync()
    {
        Console.WriteLine("=== Stream Merging with Filtering ===\n");

        var allLogs = SimulateLogSource("MixedSource", 10);
        var errorLogs = SimulateErrorLogs("ErrorSource", 3);

        // Merge with predicate filter
        var merger = new UnifiedStream<LogEntry>()
            .Unify(allLogs, "all")
            .Unify(errorLogs, "errors", predicate: log => log.Level == "ERROR");

        Console.WriteLine("Merging with filter (only ERROR from errorLogs source):\n");

        await foreach (var log in merger)
        {
            var emoji = log.Level == "ERROR" ? "🚨" : "ℹ️";
            Console.WriteLine($"  {emoji} [{log.Source}] {log.Message}");
        }

        Console.WriteLine("\n✅ Filtered merge complete");
    }

    /// <summary>
    /// Stream-Merging: Combined with Cases pattern
    /// </summary>
    public static async Task StreamMergingWithCasesAsync()
    {
        Console.WriteLine("=== Stream Merging + Cases Pattern ===\n");

        var source1 = SimulateMixedLogs("Server1", 3);
        var source2 = SimulateMixedLogs("Server2", 3);

        var unified = new UnifiedStream<LogEntry>()
            .Unify(source1, "s1")
            .Unify(source2, "s2");

        // Apply Cases pattern to merged stream
        var processed = unified
            .Cases(
                log => log.Level == "ERROR",
                log => log.Level == "WARNING"
            )
            .SelectCase(
                error => $"🚨 ALERT: [{error.Source}] {error.Message}",
                warning => $"⚠️ WARN: [{warning.Source}] {warning.Message}",
                info => $"ℹ️ INFO: [{info.Source}] {info.Message}"
            )
            .AllCases();

        Console.WriteLine("Merged and categorized output:\n");
        await foreach (var line in processed)
        {
            Console.WriteLine($"  {line}");
        }

        Console.WriteLine("\n✅ Unified stream processed with Cases pattern");
    }

    // Helper methods to simulate async log sources
    private static async IAsyncEnumerable<LogEntry> SimulateLogSource(string source, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(10); // Simulate async
            yield return new LogEntry(DateTime.Now, "INFO", $"Log entry {i + 1} from {source}", source);
        }
    }

    private static async IAsyncEnumerable<LogEntry> SimulateErrorLogs(string source, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(10);
            yield return new LogEntry(DateTime.Now, "ERROR", $"Error {i + 1}: Something went wrong", source);
        }
    }

    private static async IAsyncEnumerable<LogEntry> SimulateMixedLogs(string source, int count)
    {
        var levels = new[] { "ERROR", "WARNING", "INFO" };
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(10);
            yield return new LogEntry(DateTime.Now, levels[i % 3], $"Mixed log {i + 1}", source);
        }
    }
}
