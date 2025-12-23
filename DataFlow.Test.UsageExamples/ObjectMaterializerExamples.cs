using DataFlow.Framework;

namespace DataFlow.App.UsageExamples;

/// <summary>
/// Examples from ObjectMaterializer.md
/// Covers object creation from structured data
/// </summary>
public static class ObjectMaterializerExamples
{
    // Sample types
    public class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string Email { get; set; } = "";
    }

    public record OrderDto(string Id, decimal Amount, DateTime CreatedAt);

    /// <summary>
    /// ObjectMaterializer: Schema-Based Mapping
    /// </summary>
    public static void SchemaBasedMapping()
    {
        Console.WriteLine("=== Schema-Based Mapping ===\n");

        // Create instance using schema (public API)
        var person = ObjectMaterializer.Create<Person>(
            schema: new[] { "Name", "Age", "Email" },
            parameters: new object[] { "Alice", 30, "alice@example.com" }
        );

        Console.WriteLine($"Created person:");
        Console.WriteLine($"  Name: {person?.Name}");
        Console.WriteLine($"  Age: {person?.Age}");
        Console.WriteLine($"  Email: {person?.Email}");

        // Create another person
        var person2 = ObjectMaterializer.Create<Person>(
            new[] { "Name", "Age" },
            new object[] { "Bob", 25 }
        );

        Console.WriteLine($"\nCreated second person:");
        Console.WriteLine($"  Name: {person2?.Name}");
        Console.WriteLine($"  Age: {person2?.Age}");

        Console.WriteLine("\n✅ Schema-based mapping complete");
    }

    /// <summary>
    /// ObjectMaterializer: Constructor-Based Mapping (for records)
    /// </summary>
    public static void ConstructorBasedMapping()
    {
        Console.WriteLine("=== Constructor-Based Mapping ===\n");

        // Automatic constructor matching for records
        var dto = ObjectMaterializer.Create<OrderDto>(
            "ORD123",
            500.00m,
            DateTime.Parse("2024-01-15")
        );

        Console.WriteLine($"Created OrderDto record:");
        Console.WriteLine($"  Id: {dto?.Id}");
        Console.WriteLine($"  Amount: {dto?.Amount:C}");
        Console.WriteLine($"  CreatedAt: {dto?.CreatedAt:yyyy-MM-dd}");

        Console.WriteLine("\n✅ Records populated via constructor");
    }

    /// <summary>
    /// ObjectMaterializer: Handling Missing/Extra Columns
    /// </summary>
    public static void MissingExtraColumns()
    {
        Console.WriteLine("=== Missing/Extra Columns ===\n");

        // Missing column - Age defaults to 0
        var personMissing = ObjectMaterializer.Create<Person>(
            schema: new[] { "Name" },
            parameters: new object[] { "Alice" }
        );
        Console.WriteLine($"Missing column (Age omitted):");
        Console.WriteLine($"  Name={personMissing?.Name}, Age={personMissing?.Age} (default)");

        // Extra column - ignored
        var personExtra = ObjectMaterializer.Create<Person>(
            schema: new[] { "Name", "Age", "UnknownColumn" },
            parameters: new object[] { "Bob", 30, "ignored_value" }
        );
        Console.WriteLine($"\nExtra column (UnknownColumn ignored):");
        Console.WriteLine($"  Name={personExtra?.Name}, Age={personExtra?.Age}");

        Console.WriteLine("\n✅ Missing → default, Extra → ignored");
    }

    /// <summary>
    /// ObjectMaterializer: Bulk Test Data Creation (xUnit style)
    /// </summary>
    public static void BulkTestDataCreation()
    {
        Console.WriteLine("=== Bulk Test Data Creation ===\n");

        // Simulate CSV test data parsing
        var schema = new[] { "Name", "Age", "Email" };
        var testCases = new[]
        {
            new object[] { "Alice", 25, "alice@test.com" },
            new object[] { "Bob", 30, "bob@test.com" },
            new object[] { "Carol", 35, "carol@test.com" },
        };

        Console.WriteLine("Creating multiple test objects:");
        foreach (var row in testCases)
        {
            var person = ObjectMaterializer.Create<Person>(schema, row);
            Console.WriteLine($"  • {person?.Name} ({person?.Age}) - {person?.Email}");
        }

        Console.WriteLine($"\nTotal: {testCases.Length} objects created");
        Console.WriteLine("\n✅ Ideal for parameterized tests");
    }
}
