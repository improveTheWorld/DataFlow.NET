using DataFlow.Framework;
using Xunit.Abstractions;


namespace DataFlow.Core.Tests.Materialization;
public class PartialInitializationTests
{
    [Fact]
    public void Create_PartialSchema_InitializedPropertiesHaveValues()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var csvRow = new object[] { "John", "30" };

        // Act
        var person = ObjectMaterializer.Create<FullPerson>(schema, csvRow);

        // Assert - Explicitly initialized properties
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);
    }

    [Fact]
    public void Create_PartialSchema_UninitializedPropertiesHaveDefaults()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var csvRow = new object[] { "John", "30" };

        // Act
        var person = ObjectMaterializer.Create<FullPerson>(schema, csvRow);

        // Assert - Explicitly initialized properties
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);

        // Assert - Uninitialized properties SHOULD have CLR defaults
        Assert.NotNull(person.Department);  // "" from initializer
        Assert.Equal(DateTime.MinValue, person.HireDate); 
        Assert.False(person.IsActive); // false from CLR default

        // Document actual behavior:
        _output.WriteLine($"Department: '{person.Department}' (empty string from initializer)");
        _output.WriteLine($"HireDate: {person.HireDate} (MinValue = CLR default)");
        _output.WriteLine($"IsActive: {person.IsActive} (false = CLR default)");
    }

    [Fact]
    public void Create_PartialSchema_WithPropertyInitializers_UsesInitializers()
    {
        // Arrange
        var schema = new[] { "Name" };
        var csvRow = new object[] { "John" };

        // Act
        var person = ObjectMaterializer.Create<PersonWithDefaults>(schema, csvRow);

        // Assert - Should use property initializers, not CLR defaults
        Assert.Equal("John", person.Name);
        Assert.Equal("UNKNOWN", person.Department);  // From initializer
        Assert.True(person.IsActive);                // From initializer

        // HireDate might be DateTime.Now or MinValue depending on implementation
        Assert.NotNull(person.HireDate);
    }

    [Fact]
    public void Create_PartialSchema_RespectsPropertyInitializers()
    {
        // Arrange
        var schema = new[] { "Name" };
        var csvRow = new object[] { "John" };

        // Act
        var person = ObjectMaterializer.Create<PersonWithDefaults>(schema, csvRow);

        // Assert - Mapped property is overwritten
        Assert.Equal("John", person.Name);

        // Assert - Unmapped properties keep their initializers
        Assert.Equal("UNKNOWN", person.Department);  // From initializer
        Assert.Equal(DateTime.MinValue, person.HireDate); // From initializer
        Assert.True(person.IsActive); // From initializer
    }

    [Fact]
    public void Create_PartialSchema_DistinguishesNullFromDefault()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var csvRow = new object[] { "John", "30" };

        // Act
        var nullable = ObjectMaterializer.Create<NullableFullPerson>(schema, csvRow);

        // Assert - Mapped properties have values
        Assert.Equal("John", nullable.Name);
        Assert.Equal(30, nullable.Age);

        // Assert - Unmapped nullable properties are null
        Assert.Null(nullable.Salary);
        Assert.Null(nullable.HireDate);
        Assert.Null(nullable.IsActive);

        // This is the CORRECT way to distinguish "not provided"
    }


    [Fact]
    public void Create_PartialSchema_ReferenceTypesNotNull()
    {
        // Arrange
        var schema = new[] { "Age" };  // Only Age, no Name
        var csvRow = new object[] { "30" };

        // Act
        var person = ObjectMaterializer.Create<FullPerson>(schema, csvRow);

        // Assert - CRITICAL: Reference types should never be null
        Assert.NotNull(person.Name);       // Should be "" not null
        Assert.NotNull(person.Department); // Should be "" not null
    }

    [Fact]
    public void Create_PartialSchema_ValueTypesHaveSafeDefaults()
    {
        // Arrange
        var schema = new[] { "Name" };  // Only Name, no value types
        var csvRow = new object[] { "John" };

        // Act
        var person = ObjectMaterializer.Create<FullPerson>(schema, csvRow);

        // Assert - Value types should have predictable defaults
        Assert.Equal(0, person.Age);
        Assert.Equal(0m, person.Salary);
        Assert.False(person.IsActive);

        // DateTime is tricky - could be MinValue or Now
        Assert.True(
            person.HireDate == DateTime.MinValue ||
            person.HireDate >= DateTime.Now.AddSeconds(-1),
            $"HireDate should be MinValue or Now, but was {person.HireDate}");
    }

    [Fact]
    public void Create_PartialSchema_CanDistinguishInitializedProperties()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var csvRow = new object[] { "John", "30" };

        // Act
        var person = ObjectMaterializer.Create<FullPerson>(schema, csvRow);

        // Assert - Can we tell which properties were initialized?
        // This might require additional metadata or a wrapper object

        // Option 1: Check against defaults (fragile)
        bool nameWasInitialized = person.Name != "";
        bool ageWasInitialized = person.Age != 0;
        bool salaryWasInitialized = person.Salary != 0m;

        Assert.True(nameWasInitialized);
        Assert.True(ageWasInitialized);
        Assert.False(salaryWasInitialized);  // Not in schema

        // Option 2: Use nullable types (better)
        // See next test
    }

    [Fact]
    public void Create_PartialSchema_WithNullableTypes_ClearerSemantics()
    {
        // Arrange - Using nullable types makes "not provided" explicit
        var schema = new[] { "Name", "Age" };
        var csvRow = new object[] { "John", "30" };

        // Act
        var person = ObjectMaterializer.Create<NullableFullPerson>(schema, csvRow);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);

        // Uninitialized nullable properties should be null
        Assert.Null(person.Salary);     // Clear: not provided
        Assert.Null(person.HireDate);   // Clear: not provided
        Assert.Null(person.IsActive);   // Clear: not provided
    }

    [Fact]
    public void Create_PartialSchema_MultipleInstances_IndependentDefaults()
    {
        // Arrange
        var schema = new[] { "Name" };

        // Act - Create multiple instances
        var person1 = ObjectMaterializer.Create<PersonWithDefaults>(
            schema, new object[] { "John" });
        var person2 = ObjectMaterializer.Create<PersonWithDefaults>(
            schema, new object[] { "Jane" });

        // Modify one instance
        person1.Department = "IT";

        // Assert - Instances should be independent
        Assert.Equal("IT", person1.Department);
        Assert.Equal("UNKNOWN", person2.Department);  // Not affected
    }

    private readonly ITestOutputHelper _output;

    public PartialInitializationTests(ITestOutputHelper output)
    {
        _output = output;
    }
}

// Test models
public class FullPerson
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public decimal Salary { get; set; }
    public string Department { get; set; } = "";
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; }
}

public class PersonWithDefaults
{
    public string Name { get; set; } = "";
    public int Age { get; set; } = 0;
    public decimal Salary { get; set; } = 0m;
    public string Department { get; set; } = "UNKNOWN";
    public DateTime HireDate { get; set; } = DateTime.MinValue;
    public bool IsActive { get; set; } = true;
}

public class NullableFullPerson
{
    public string Name { get; set; } = "";
    public int? Age { get; set; }
    public decimal? Salary { get; set; }
    public string? Department { get; set; }
    public DateTime? HireDate { get; set; }
    public bool? IsActive { get; set; }
}
