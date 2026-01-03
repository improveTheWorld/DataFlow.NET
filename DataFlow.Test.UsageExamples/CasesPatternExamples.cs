using DataFlow;
using DataFlow.Framework;
using DataFlow.Extensions;
using DataFlow.Parallel;
namespace DataFlow.App.UsageExamples;

/// <summary>
/// Examples from Unified-Processing.md - Cases/SelectCase/ForEachCase pattern
/// </summary>
public static class CasesPatternExamples
{
    // Sample data types
    public record Transaction(int Id, decimal Amount, bool IsFlagged, string Country);
    public record Order(int Id, bool IsUrgent, bool IsInternational, decimal Amount);
    public record DataRecord(string Type, string Data);

    /// <summary>
    /// Unified-Processing: Configuration-Driven Transformation Tree
    /// Demonstrates: Cases → SelectCase → ForEachCase → AllCases
    /// </summary>
    public static void ConfigurationDrivenTransformationTree()
    {
        Console.WriteLine("=== Configuration-Driven Transformation Tree ===\n");

        var data = new[]
        {
            new DataRecord("Customer", "John Doe"),
            new DataRecord("Order", "ORD-12345"),
            new DataRecord("Product", "Widget Pro"),
            new DataRecord("Unknown", "???"),
            new DataRecord("Customer", "Jane Smith")
        };

        // Configure complete transformation tree ONCE
        var results = data
            .Cases(
                d => d.Type == "Customer",
                d => d.Type == "Order",
                d => d.Type == "Product"
            )
            .SelectCase(
                customer => $"[CUSTOMER] Enriched: {customer.Data.ToUpper()}",
                order => $"[ORDER] Calculated: {order.Data}",
                product => $"[PRODUCT] Normalized: {product.Data}",
                unknown => $"[UNKNOWN] Logged: {unknown.Data}"  // Supra category
            )
            .ForEachCase(
                customer => Console.WriteLine($"  → Saved customer to CustomerDB"),
                order => Console.WriteLine($"  → Saved order to OrderDB"),
                product => Console.WriteLine($"  → Saved product to ProductDB"),
                unknown => Console.WriteLine($"  → Logged unknown to ErrorLog")
            )
            .AllCases()
            .ToList();

        Console.WriteLine("\nTransformed data:");
        foreach (var item in results)
            Console.WriteLine($"  {item}");

        Console.WriteLine("\n✅ Tree configured once, executed lazily");
        Console.WriteLine("✅ Each item processed exactly once");
    }

    /// <summary>
    /// Unified-Processing: The Supra Category Pattern
    /// Demonstrates: Handling unmatched items gracefully
    /// </summary>
    public static void SupraCategoryPattern()
    {
        Console.WriteLine("=== Supra Category Pattern ===\n");

        var transactions = new[]
        {
            new Transaction(1, 15000, false, "US"),
            new Transaction(2, 500, true, "US"),      // Flagged
            new Transaction(3, 8000, false, "UK"),    // International
            new Transaction(4, 200, false, "US"),     // Standard (matches no predicate)
            new Transaction(5, 25000, false, "US"),
        };

        Console.WriteLine("Processing transactions with Supra category...\n");

        var results = transactions
            .Cases(
                tx => tx.Amount > 10000,    // Case 0: High-value
                tx => tx.IsFlagged,         // Case 1: Suspicious
                tx => tx.Country != "US"    // Case 2: International
                // Supra: everything else (standard transactions)
            )
            .SelectCase(
                highValue => $"🔴 HIGH VALUE: Tx#{highValue.Id} ${highValue.Amount}",
                suspicious => $"🟠 SUSPICIOUS: Tx#{suspicious.Id} FLAGGED",
                international => $"🟡 INTERNATIONAL: Tx#{international.Id} [{international.Country}]",
                standard => $"🟢 STANDARD: Tx#{standard.Id} ${standard.Amount}"  // ← Supra handler
            )
            .AllCases()
            .ToList();

        foreach (var line in results)
            Console.WriteLine($"  {line}");

        Console.WriteLine("\n✅ All items handled - no data lost!");
    }

    /// <summary>
    /// Unified-Processing: Sync vs Async unified processing
    /// </summary>
    public static async Task SyncAsyncUnifiedAsync()
    {
        Console.WriteLine("=== Sync/Async Unified Processing ===\n");

        var orders = new[]
        {
            new Order(1, true, false, 1500),
            new Order(2, false, true, 800),
            new Order(3, false, false, 3000),
        };

        Console.WriteLine("Sync processing (IEnumerable):");
        orders
            .Cases(o => o.IsUrgent, o => o.IsInternational)
            .SelectCase(
                urgent => $"  URGENT: Order #{urgent.Id}",
                intl => $"  INTL: Order #{intl.Id}",
                std => $"  STD: Order #{std.Id}"
            )
            .AllCases()
            .ToList()
            .ForEach(Console.WriteLine);

        Console.WriteLine("\nAsync processing (IAsyncEnumerable):");
        await foreach (var line in orders.Async()
            .Cases(o => o.IsUrgent, o => o.IsInternational)
            .SelectCase(
                urgent => $"  URGENT: Order #{urgent.Id}",
                intl => $"  INTL: Order #{intl.Id}",
                std => $"  STD: Order #{std.Id}"
            )
            .AllCases())
        {
            Console.WriteLine(line);
        }

        Console.WriteLine("\n✅ Same syntax for both sync and async!");
    }
}
