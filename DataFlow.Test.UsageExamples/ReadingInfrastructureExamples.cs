using DataFlow;

namespace App.UsageExamples;

/// <summary>
/// Examples from DataFlow-Data-Reading-Infrastructure.md
/// Covers advanced CSV, JSON, YAML reading with options
/// </summary>
public static class ReadingInfrastructureExamples
{
    public record MyRow(string Id, string Name, decimal Price);
    public record Config(string Key, string Value);

    /// <summary>
    /// Reading-Infra 0.1: Async vs Sync naming convention
    /// </summary>
    public static async Task AsyncSyncConventionAsync()
    {
        Console.WriteLine("=== Async/Sync Naming Convention ===\n");

        // Create sample file
        var sampleLines = new[] { "Line 1", "Line 2", "Line 3" };
        await File.WriteAllLinesAsync("sample.txt", sampleLines);

        // Async: Read.Text() returns IAsyncEnumerable<string>
        Console.WriteLine("Async reading:");
        await foreach (var line in Read.Text("sample.txt"))
        {
            Console.WriteLine($"  [Async] {line}");
        }

        // Sync: Read.TextSync() returns IEnumerable<string>
        Console.WriteLine("\nSync reading:");
        foreach (var line in Read.TextSync("sample.txt"))
        {
            Console.WriteLine($"  [Sync] {line}");
        }

        File.Delete("sample.txt");
        Console.WriteLine("\n✅ Async uses default names, Sync uses 'Sync' suffix");
    }

    /// <summary>
    /// Reading-Infra 0.2: String-based parsing (extension methods)
    /// </summary>
    public static void StringBasedParsing()
    {
        Console.WriteLine("=== String-Based Parsing ===\n");

        // CSV from string
        var csvText = @"Id,Name,Price
1,Widget,19.99
2,Gadget,29.99
3,Device,39.99";

        Console.WriteLine("Parsing CSV from string:");
        foreach (var row in csvText.AsCsv<MyRow>(","))
        {
            Console.WriteLine($"  {row.Id}: {row.Name} @ {row.Price:C}");
        }

        // JSON from string
        var jsonText = @"[
            {""Key"": ""setting1"", ""Value"": ""enabled""},
            {""Key"": ""setting2"", ""Value"": ""disabled""}
        ]";

        Console.WriteLine("\nParsing JSON from string:");
        foreach (var item in jsonText.AsJson<Config>())
        {
            Console.WriteLine($"  {item.Key} = {item.Value}");
        }

        Console.WriteLine("\n✅ Direct parsing from strings using extension methods");
    }

    /// <summary>
    /// Reading-Infra 0.5: Simple CSV with error handling
    /// </summary>
    public static void SimpleCsvWithErrorHandling()
    {
        Console.WriteLine("=== Simple CSV with Error Handling ===\n");

        // Create a CSV with some bad rows
        var csvContent = @"Id,Name,Price
1,Widget,19.99
2,Gadget,invalid_price
3,Device,39.99
extra,columns,here,oops";

        File.WriteAllText("test_errors.csv", csvContent);

        int errorCount = 0;

        Console.WriteLine("Reading CSV with error callback:");
        var rows = Read.CsvSync<MyRow>(
            "test_errors.csv",
            ",",
            onError: (rawLine, ex) =>
            {
                errorCount++;
                Console.WriteLine($"  ⚠️ Error on row: {rawLine.Substring(0, Math.Min(30, rawLine.Length))}...");
            }
        ).ToList();

        Console.WriteLine($"\nSuccessfully parsed {rows.Count} rows");
        Console.WriteLine($"Skipped {errorCount} rows with errors");

        foreach (var row in rows)
        {
            Console.WriteLine($"  ✓ {row.Id}: {row.Name} @ {row.Price:C}");
        }

        File.Delete("test_errors.csv");
        Console.WriteLine("\n✅ Error rows skipped, valid rows processed");
    }

    /// <summary>
    /// Reading-Infra 0.4: Read raw text lines
    /// </summary>
    public static async Task RawTextLinesAsync()
    {
        Console.WriteLine("=== Raw Text Lines ===\n");

        var logContent = @"2024-01-01 INFO Application started
2024-01-01 WARNING High memory usage
2024-01-01 ERROR Connection timeout
2024-01-01 INFO Request processed";

        await File.WriteAllTextAsync("app.log", logContent);

        Console.WriteLine("Reading log file line by line:");
        await foreach (var line in Read.Text("app.log"))
        {
            var emoji = line.Contains("ERROR") ? "🚨" :
                       line.Contains("WARNING") ? "⚠️" : "ℹ️";
            Console.WriteLine($"  {emoji} {line}");
        }

        File.Delete("app.log");
        Console.WriteLine("\n✅ Each line streamed individually");
    }
}
