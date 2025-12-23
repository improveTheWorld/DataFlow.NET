using DataFlow.Extensions;
using DataFlow.Framework;
using DataFlow.Data;
using static DataFlow.Framework.RegexWrap;

namespace DataFlow.App.UsageExamples.legacyExamples;

/// <summary>
/// Legacy examples from the original Program.cs - preserved for backwards compatibility
/// </summary>
public static class LegacyExamples
{
    struct Person
    {
        public string FirstName;
        public string Name;
        public int Age;
    }

    public static void CsvSimpleExample()
    {
        Console.WriteLine("=== Legacy: CSV Simple Example ===\n");

        // Read lines from a CSV file, extract data, and fill into an Enumerable of Person
        Read.CsvSync<Person>("People.csv", ",")
            .Take(5)
            // Convert names to uppercase
            .Select(p =>
            {
                p.Name = p.Name.ToUpper();
                return p;
            })
            .ToList()
            .ForEach(p => Console.WriteLine($"  {p.FirstName} {p.Name}, Age {p.Age}"));

        Console.WriteLine("\n✅ CSV processed (first 5 records shown)");
    }

    public static void TextAdvancedExample()
    {
        Console.WriteLine("=== Legacy: Text Advanced Example ===\n");

        // Read lines and categorize by log level
        var results = Read.TextSync("log.txt")
            .Until(line => line.StartsWith("STOP:"))
            .Cases(
                line => line.ToUpper().Contains("ERROR"),
                line => line.ToUpper().Contains("WARNING")
            )
            // Apply suitable transformations for each category,
            // => Add Log level information at the beginning of each line
            .SelectCase(
                line => $"ERROR : {line}",   // for lines containing "error" 
                line => $"WARNING : {line}", // for lines containing "warning"
                line => $"INFO : {line}"     // for other lines, assume Info
            )
            // Collect to show results
            .AllCases()
            .Take(10)
            .ToList();

        Console.WriteLine("Categorized log entries (first 10):");
        foreach (var line in results)
        {
            Console.WriteLine($"  {line}");
        }

        Console.WriteLine("\n✅ Text categorization complete");
    }

    public static void RgxsUsageExample()
    {
        Console.WriteLine("=== Legacy: Regex Tokenizer Example ===\n");

        var log = new List<string>() {
            "Low memory condition detected while running application, this is a warning",
            "Server Status OK,  Received Response : 500 server error",
            "Received Response : 200 Status ok" ,
            "Resource allocation exceeded for process ID 453." ,
            "Received Response : 404 not Found"
        }.PutInStream();

        int errorsCount = 0;

        // Read the log file and analyze each line
        var results = Read.TextSync(log.BaseStream)
            // Define regex patterns for different log entry types
            .TokenAndFlatten($"Received Response : {NUMS.As("ErrorCode")} {WORDS.As("errorMessage")}")
            .Cases(
                "ErrorCode",
                "errorMessage"
            )
            .SelectCase(
                code =>
                {
                    if (code != "200") errorsCount++;
                    return code;
                },
                message => $"--{message.ToUpper()}--",
                x => x
            )
            .AllCases()
            .ToList();

        Console.WriteLine("Tokenized results:");
        foreach (var item in results)
        {
            Console.WriteLine($"  {item}");
        }

        Console.WriteLine();
        Console.WriteLine($"Number of errors: {errorsCount}");
        Console.WriteLine("\n✅ Regex tokenization complete");
    }
}
public static class InMemory
{
    public static StreamReader PutInStream(this IEnumerable<string> lines)
    {
        MemoryStream stream = new MemoryStream();

        var writer = new StreamWriter(stream);

        lines.ForEach(line => writer.WriteLine(line)).Do();

        writer.Flush(); // Ensures all data is written to the stream

        stream.Position = 0; // Reset the position to the beginning of the stream

        return new StreamReader(stream);

    }

}