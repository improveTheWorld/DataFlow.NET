using DataFlow;

namespace App.UsageExamples;

/// <summary>
/// Examples from Readme.md - Quick demonstration of core capabilities
/// </summary>
public static class ReadmeExamples
{
    // Sample data types
    public record Log(string Level, string Message, string Service);
    public record Order(int Id, decimal Amount, bool IsInternational);

    /// <summary>
    /// Readme: "Write Once, Process Anywhere" - Cases pattern for batch processing
    /// </summary>
    public static async Task CasesPatternBatchAsync()
    {
        Console.WriteLine("=== Cases Pattern - Batch Processing ===\n");

        // Simulate log entries
        var logs = new[]
        {
            new Log("ERROR", "Database connection failed", "OrderService"),
            new Log("WARNING", "High memory usage detected", "PaymentService"),
            new Log("INFO", "User logged in", "AuthService"),
            new Log("ERROR", "Payment timeout", "PaymentService"),
            new Log("INFO", "Order processed", "OrderService")
        };

        // Cases pattern: categorize and transform
        var results = logs
            .Cases(
                log => log.Level == "ERROR",
                log => log.Level == "WARNING"
            )
            .SelectCase(
                error => $"🚨 [{error.Service}] {error.Message}",
                warning => $"⚠️ [{warning.Service}] {warning.Message}",
                info => $"ℹ️ [{info.Service}] {info.Message}"
            )
            .AllCases()
            .ToList();

        Console.WriteLine("Processed logs:");
        foreach (var line in results)
            Console.WriteLine($"  {line}");

        Console.WriteLine($"\nTotal: {results.Count} entries processed");
    }

    /// <summary>
    /// Readme: CSV processing with Cases pattern
    /// </summary>
    public static void QuickStartCsvProcessing()
    {
        Console.WriteLine("=== Quick Start - CSV Processing ===\n");

        // Simulate orders (in real use: Read.Csv<Order>("orders.csv"))
        var orders = new[]
        {
            new Order(1, 15000, false),
            new Order(2, 500, true),
            new Order(3, 25000, false),
            new Order(4, 800, false),
            new Order(5, 12000, true)
        };

        var processed = orders
            .Cases(
                o => o.Amount > 10000,
                o => o.IsInternational
            )
            .SelectCase(
                highValue => $"HIGH VALUE: Order #{highValue.Id} = {highValue.Amount:C}",
                international => $"INTERNATIONAL: Order #{international.Id} = {international.Amount:C}",
                standard => $"STANDARD: Order #{standard.Id} = {standard.Amount:C}"
            )
            .AllCases()
            .ToList();

        Console.WriteLine("Categorized orders:");
        foreach (var line in processed)
            Console.WriteLine($"  {line}");
    }
}
