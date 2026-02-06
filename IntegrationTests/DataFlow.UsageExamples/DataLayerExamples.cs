using DataFlow;
namespace App.UsageExamples;

/// <summary>
/// Examples from Architecture-APIs.md and DataFlow-Data-Layer.md
/// </summary>
public static class DataLayerExamples
{
    public record Person(string FirstName, string Name, int Age);
    public record LogEntry(DateTime Timestamp, string Level, string Message);

    /// <summary>
    /// Technical-Doc: Read Class - Sync text reading
    /// </summary>
    public static void TextReadingSync()
    {
        Console.WriteLine("=== Text Reading (Sync) ===\n");

        // Create sample data
        var sampleLines = new[] { "Line 1: Hello World", "Line 2: DataFlow.NET", "Line 3: Examples" };
        File.WriteAllLines("sample_text.txt", sampleLines);

        // Read text synchronously
        Console.WriteLine("Reading text file line by line:");
        foreach (var line in Read.TextSync("sample_text.txt"))
        {
            Console.WriteLine($"  → {line}");
        }

        // Cleanup
        File.Delete("sample_text.txt");
        Console.WriteLine("\n✅ Sync text reading complete");
    }

    /// <summary>
    /// Technical-Doc: Read Class - Async text reading
    /// </summary>
    public static async Task TextReadingAsync()
    {
        Console.WriteLine("=== Text Reading (Async) ===\n");

        var sampleLines = new[] { "Async Line 1", "Async Line 2", "Async Line 3" };
        await File.WriteAllLinesAsync("sample_async.txt", sampleLines);

        Console.WriteLine("Reading text file asynchronously:");
        await foreach (var line in Read.Text("sample_async.txt"))
        {
            Console.WriteLine($"  → {line}");
        }

        File.Delete("sample_async.txt");
        Console.WriteLine("\n✅ Async text reading complete");
    }

    /// <summary>
    /// Technical-Doc: CSV reading with type mapping using inline data
    /// </summary>
    public static void CsvReadingSync()
    {
        Console.WriteLine("=== CSV Reading (Sync) ===\n");

        // Using inline string data with AsCsv() - no external file needed
        var csvData = """
            FirstName,Name,Age
            John,Doe,30
            Jane,Smith,25
            Bob,Wilson,45
            Alice,Brown,28
            Charlie,Davis,35
            """;

        Console.WriteLine("Reading inline CSV and transforming:");

        var people = csvData.AsCsv<Person>(",")
            .Take(5)
            .Select(p => $"  {p.FirstName} {p.Name}, Age: {p.Age}")
            .ToList();

        foreach (var line in people)
            Console.WriteLine(line);

        Console.WriteLine("\n✅ CSV reading complete (first 5 records)");
    }

    /// <summary>
    /// Technical-Doc: Writers - CSV writing
    /// </summary>
    public static void CsvWriting()
    {
        Console.WriteLine("=== CSV Writing ===\n");

        var people = new[]
        {
            new Person("John", "DOE", 30),
            new Person("Jane", "SMITH", 25),
            new Person("Bob", "WILSON", 45)
        };

        Console.WriteLine("Writing people to output.csv:");
        people.WriteCsvSync("output_people.csv", true);

        // Verify
        Console.WriteLine("\nVerifying written file:");
        foreach (var line in Read.TextSync("output_people.csv"))
        {
            Console.WriteLine($"  {line}");
        }

        File.Delete("output_people.csv");
        Console.WriteLine("\n✅ CSV writing complete");
    }

    /// <summary>
    /// Technical-Doc: Utility extensions - Until, Spy, Cumul
    /// </summary>
    public static void UtilityExtensions()
    {
        Console.WriteLine("=== Utility Extensions ===\n");

        var numbers = Enumerable.Range(1, 20);

        // Until - stop when condition met
        Console.WriteLine("Until (stop at > 10):");
        numbers.Until(n => n > 10)
            .ToList()
            .ForEach(n => Console.Write($"{n} "));
        Console.WriteLine("\n");

        // Take first 5 numbers
        Console.WriteLine("Take first 5:");
        var first5 = numbers.Take(5).ToList();
        Console.WriteLine($"  Numbers: {string.Join(", ", first5)}");

        Console.WriteLine("\n✅ Utility extensions demonstrated");
    }
}
