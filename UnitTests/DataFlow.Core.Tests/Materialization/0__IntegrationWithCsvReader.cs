using DataFlow.Framework;
using DataFlow;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

/// <summary>
/// Tests that simulate real CSV reader integration scenarios
/// </summary>
public class CsvReaderIntegrationTests
{
    [Fact]
    public void MaterializeFromCsvRow_WithTypicalData_ShouldWork()
    {
        // Arrange - Simulate CSV reader output
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object[] { "John Doe", "30", "75000.50" }; // CSV values are strings

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);

        // Assert
        Assert.Equal("John Doe", person.Name);
        Assert.Equal(30, person.Age);
        Assert.Equal(75000.50m, person.Salary);
    }

    [Fact]
    public void MaterializeFromCsvRow_WithEmptyFields_ShouldHandleGracefully()
    {
        // Arrange - Simulate CSV with empty fields
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object?[] { "", null, "" };

        // Act
        var person = ObjectMaterializer.Create<NullableFields>(schema, csvRow);

        // Assert
        Assert.Equal("", person.Name);
        Assert.Null(person.Age);
        Assert.Null(person.Salary);
    }

    [Fact]
    public void MaterializeFromCsvRow_WithQuotedStrings_ShouldPreserveContent()
    {
        // Arrange - CSV with quoted strings (quotes already removed by parser)
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object[] { "Doe, John \"Johnny\"", "30", "75000" };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);

        // Assert
        Assert.Equal("Doe, John \"Johnny\"", person.Name);
    }

    [Fact]
    public void MaterializeFromCsvRow_WithMissingColumns_ShouldUseDefaults()
    {
        // Arrange - CSV with fewer columns than object properties
        var schema = new[] { "Name", "Age" };
        var csvRow = new object[] { "John", "30" };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);
        Assert.Equal(0m, person.Salary); // Default
    }

    [Fact]
    public void MaterializeFromCsvRow_WithExtraColumns_ShouldIgnore()
    {
        // Arrange - CSV with more columns than needed
        var schema = new[] { "Name", "Age", "Salary", "Department", "Location" };
        var csvRow = new object[] { "John", "30", "75000", "IT", "NYC" };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);
        Assert.Equal(75000m, person.Salary);
        // Department and Location ignored (not in PersonMutable)
    }

    [Fact]
    public void MaterializeFromCsvRow_WithHeaderCaseVariations_ShouldMatch()
    {
        // Arrange - Real-world CSV headers often have inconsistent casing
        var schema = new[] { "FULLNAME", "personage", "AnnualSalary" };
        var csvRow = new object[] { "John Doe", "30", "75000" };
        schema = ObjectMaterializer.ResolveSchema<FlexibleHeaderModel>(schema);
        // Act - Using case-insensitive mapping
        var obj = ObjectMaterializer.Create<FlexibleHeaderModel>(schema, csvRow);

        // Assert
        Assert.Equal("John Doe", obj.FullName);
        Assert.Equal(30, obj.PersonAge);
        Assert.Equal(75000m, obj.AnnualSalary);
    }

    [Fact]
    public void MaterializeFromCsvRow_WithNumericFormats_ShouldParse()
    {
        // Arrange - CSV with various numeric formats
        var schema = new[] { "IntValue", "LongValue", "DecimalValue", "DoubleValue" };
        var csvRow = new object[] { "1000", "9999999999", "1234.56", "99.99" };

        // Act
        var obj = ObjectMaterializer.Create<NumericTypesModel>(schema, csvRow);

        // Assert
        Assert.Equal(1000, obj.IntValue);
        Assert.Equal(9999999999L, obj.LongValue);
        Assert.Equal(1234.56m, obj.DecimalValue);
        Assert.Equal(99.99, obj.DoubleValue);
    }

    [Fact]
    public void MaterializeFromCsvRow_WithDateFormats_ShouldParse()
    {
        // Arrange - CSV with date strings
        var schema = new[] { "BirthDate", "HireDate" };
        var csvRow = new object[] { "1990-05-15", "2020-01-01" };

        // Act
        var obj = ObjectMaterializer.Create<DateModel>(schema, csvRow);

        // Assert
        Assert.Equal(new DateTime(1990, 5, 15), obj.BirthDate);
        Assert.Equal(new DateTime(2020, 1, 1), obj.HireDate);
    }

    [Fact]
    public void MaterializeFromCsvRow_ErrorScenario_ShouldProvideContext()
    {
        // Arrange - Invalid data that should fail
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object[] { "John", "invalid-age", "75000" };

        schema = ObjectMaterializer.ResolveSchema<PersonMutable>(schema);
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
      ObjectMaterializer.Create<PersonMutable>(schema, csvRow));

        //Assert.IsType<FormatException>(ex.InnerException);
        Assert.Contains("invalid-age", ex.InnerException.Message);
    }

    [Fact]
    public void MaterializeFromCsvRow_HighVolume_ShouldMaintainPerformance()
    {
        // Arrange - Simulate processing 10k CSV rows
        var schema = new[] { "Name", "Age", "Salary" };
        const int rowCount = 10000;

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < rowCount; i++)
        {
            var csvRow = new object[] { $"Person{i}", $"{20 + (i % 50)}", $"{50000 + i}" };
            var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);
        }
        sw.Stop();

        // Assert
        _output.WriteLine($"Processed {rowCount} rows in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average: {(double)sw.ElapsedMilliseconds / rowCount:F3}ms per row");

        // Should process at least 1000 rows per second
        Assert.True(sw.ElapsedMilliseconds < rowCount,
            $"Processing {rowCount} rows took {sw.ElapsedMilliseconds}ms (should be under {rowCount}ms)");
    }

    [Fact]
    public void MaterializeFromCsvRow_WithEmptyAge_ShouldDefaultToZero()
    {
        // Arrange - Document the lenient behavior
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object[] { "Jane", "", "50000" };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);

        // Assert
        Assert.Equal("Jane", person.Name);
        Assert.Equal(0, person.Age);  // DOCUMENTED: Empty → default(int)
        Assert.Equal(50000m, person.Salary);
    }

    [Fact]
    public void MaterializeFromCsvRow_WithNullAge_ShouldDefaultToZero()
    {
        // Arrange - Document null handling
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object?[] { "Jane", null, "50000" };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);

        // Assert
        Assert.Equal("Jane", person.Name);
        Assert.Equal(0, person.Age);  // DOCUMENTED: null → default(int)
        Assert.Equal(50000m, person.Salary);
    }

    [Fact]
    public void MaterializeFromCsvRow_WithWhitespaceAge_ShouldDefaultToZero()
    {
        // Arrange - Document whitespace handling
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object[] { "Jane", "   ", "50000" };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, csvRow);

        // Assert
        Assert.Equal("Jane", person.Name);
        Assert.Equal(0, person.Age);  // DOCUMENTED: whitespace → default(int)
        Assert.Equal(50000m, person.Salary);
    }

    [Fact]
    public void MaterializeFromCsvRow_CanFilterDefaultValues()
    {
        // Arrange - Show how users can detect default values
        var schema = new[] { "Name", "Age", "Salary" };
        var rows = new[]
        {
        new object[] { "John", "30", "75000" },
        new object[] { "Jane", "", "50000" },      // Empty age
        new object[] { "Bob", "25", "" },          // Empty salary
        new object[] { "Alice", "35", "60000" }
    };

        // Act
        var people = rows.Select(row =>
            ObjectMaterializer.Create<PersonMutable>(schema, row)).ToList();

        // Filter out rows with default values (business rule)
        var validPeople = people.Where(p => p.Age > 0 && p.Salary > 0).ToList();

        // Assert
        Assert.Equal(4, people.Count);        // All rows materialized
        Assert.Equal(2, validPeople.Count);   // Only valid rows (John, Alice)
    }

    [Fact]
    public void MaterializeFromCsvRow_WithNullableFields_EmptyShouldBeNull()
    {
        // Arrange - Nullable fields should handle empty as null
        var schema = new[] { "Name", "Age", "Salary" };
        var csvRow = new object[] { "Jane", "", "" };

        // Act
        var person = ObjectMaterializer.Create<NullableFields>(schema, csvRow);

        // Assert
        Assert.Equal("Jane", person.Name);
        Assert.Null(person.Age);      // Nullable int: empty → null
        Assert.Null(person.Salary);   // Nullable decimal: empty → null
    }

    [Fact]
    public void CsvSync_EmptyStringForInt_DefaultBehavior_IncludesRow()
    {
        // Arrange
        var csv = "Name,Age\nJohn,\nJane,25\n";
        var opts = new CsvReadOptions { ErrorAction = ReaderErrorAction.Skip };

        // Act
        var results = Read.AsCsvSync<Model>(csv, opts).ToList();

        // Assert - Lenient behavior
        Assert.Equal(2, results.Count);
        Assert.Equal("John", results[0].Name);
        Assert.Equal(0, results[0].Age);  // Empty string → 0
        Assert.Equal("Jane", results[1].Name);
        Assert.Equal(25, results[1].Age);
    }


    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public CsvReaderIntegrationTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }
}

// Additional test models
public class FlexibleHeaderModel
{
    public string FullName { get; set; } = "";
    public int PersonAge { get; set; }
    public decimal AnnualSalary { get; set; }
}

public class NumericTypesModel
{
    public int IntValue { get; set; }
    public long LongValue { get; set; }
    public decimal DecimalValue { get; set; }
    public double DoubleValue { get; set; }
}

public class DateModel
{
    public DateTime BirthDate { get; set; }
    public DateTime HireDate { get; set; }
}
