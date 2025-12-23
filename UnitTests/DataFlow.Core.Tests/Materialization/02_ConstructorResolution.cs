using DataFlow.Framework;
using DataFlow.Data;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

public class ConstructorResolutionTests
{
    /// <summary>
    /// Scoring system selects exact match
    /// </summary>
    [Fact]
    public void Create_WithExactMatchConstructor_ShouldPreferExactMatch()
    {
        // Arrange
        var values = new object[] { "Test", 42 };

        // Act
        var obj = ObjectMaterializer.Create<MultipleViableConstructors>(values);

        // Assert
        Assert.NotNull(obj);
        Assert.Equal("Test", obj.Name);
        Assert.Equal(42, obj.Age);

        // CRITICAL: Should prefer exact match (string, int) over widening (string, long)
        Assert.Equal("exact", obj.Source);
    }

    /// <summary>
    /// Prefers widening over generic
    /// </summary>
    [Fact]
    public void Create_WithWideningConversion_ShouldPreferWideningOverGeneric()
    {
        // Arrange
        var values = new object[] { "Test", 42L }; // long literal

        // Act
        var obj = ObjectMaterializer.Create<MultipleViableConstructors>(values);

        // Assert
        Assert.NotNull(obj);

        // Should prefer (string, long) over (object, object)
        Assert.Equal("widening", obj.Source);
    }

    /// <summary>
    /// Consistent selection based on scoring
    /// </summary>
    [Fact]
    public void Create_WithAmbiguousConstructors_ShouldBeConsistent()
    {
        // Arrange
        var stringArg = new object[] { "test-string" };
        var intArg = new object[] { 123 };

        // Act
        var fromString1 = ObjectMaterializer.Create<AmbiguousConstructors>(stringArg);
        var fromString2 = ObjectMaterializer.Create<AmbiguousConstructors>(stringArg);
        var fromInt1 = ObjectMaterializer.Create<AmbiguousConstructors>(intArg);
        var fromInt2 = ObjectMaterializer.Create<AmbiguousConstructors>(intArg);

        // Assert - Same input should ALWAYS produce same constructor selection
        Assert.Equal(fromString1.Number, fromString2.Number);
        Assert.Equal(fromInt1.Number, fromInt2.Number);

        // Different types should select different constructors
        Assert.NotEqual(fromString1.Number, fromInt1.Number);
    }

    /// <summary>
    /// Gracefully falls back to generic constructor
    /// </summary>
    [Fact]
    public void Create_WithNoExactMatch_ShouldFallbackToGenericConstructor()
    {
        // Arrange - Pass a type not directly matching any constructor
        var values = new object[] { new DateTime(2025, 1, 1) };

        // Act
        var obj = ObjectMaterializer.Create<AmbiguousConstructors>(values);

        // Assert
        Assert.NotNull(obj);
        Assert.Equal(-2, obj.Number); // From object constructor
        Assert.Contains("2025", obj.Value);
    }

    [Fact]
    public void Create_WithNullInConstructor_ShouldHandleGracefully()
    {
        // Arrange
        var values = new object?[] { null };

        // Act
        var obj = ObjectMaterializer.Create<AmbiguousConstructors>(values);

        // Assert
        Assert.NotNull(obj); // The object itself should be created
        Assert.Null(obj.Value); // But the Value property should be null
    }

    /// <summary>
    /// Signature key includes null position
    /// </summary>
    [Fact]
    public void Create_WithNullAtDifferentPositions_ShouldCacheCorrectly()
    {
        // Arrange
        var values1 = new object?[] { null, 42 };
        var values2 = new object?[] { "test", null };

        // Act
        var obj1 = ObjectMaterializer.Create<MultipleViableConstructors>(values1);
        var obj2 = ObjectMaterializer.Create<MultipleViableConstructors>(values2);

        // Assert - Both should work despite null in different positions
        Assert.NotNull(obj1);
        Assert.NotNull(obj2);
        Assert.Null(obj1.Name); // Null stays null
        Assert.Equal("test", obj2.Name);
    }

    [Fact]
    public void Create_WithNoMatchingConstructor_ShoulChooseGenericConstructor()
    {
        // Arrange
        var values = new object[] { "TestName", "not-an-int" };

        // Act - Should NOT throw, should fallback to member feeding
        var result = ObjectMaterializer.Create<MultipleViableConstructors>(values);

        // Assert
        Assert.NotNull(result);
        // Verify it used member feeding (check which members got set based on Order attributes)
    }
    [Fact]
    public void Create_WithInvalidTypes_UsesGenericConstructorFallback()
    {
        var values = new object[] { "name", "not-an-int" };
        var result = ObjectMaterializer.Create<MultipleViableConstructors>(values);

        Assert.Equal("generic", result.Source);
    }

    [Fact]
    public void Create_WithNoViableConstructor_ThrowsMeaningfulException()
    {
        // Use a class that truly has no matching constructor
        var values = new object[] { "name", 123 };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.Create<StrictConstructorOnly>(values));

        Assert.Contains("No matching constructor", ex.Message, StringComparison.CurrentCultureIgnoreCase);
    }

    // Test helper class with no generic fallback



    [Fact]
    public void CreateOrFeed_WithGenericConstructor_ShouldUseGenericOverload()
    {
        // Arrange
        var values = new object[] { "name", "not-an-int" };

        // Act
        var result = ObjectMaterializer.CreateOrFeed<MultipleViableConstructors>(
            values,
            allowFeedFallback: false);

        // Assert
        Assert.Equal("generic", result.Source);
        Assert.Equal("name", result.Name);
        Assert.Equal(0, result.Age); // Defaults to 0 since "not-an-int" can't convert
    }

    [Fact]
    public void CreateOrFeed_WithStrictMode_ShouldThrowWhenNoConstructorMatches()
    {
        var values = new object[] { "name", "not-an-int" };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.CreateOrFeed<StrictConstructors>(
                values,
                allowFeedFallback: false));

        Assert.Contains("No matching constructor", ex.Message);
    }
}


// Basic models
public record Person(string Name, int Age, decimal Salary);

public class PersonMutable
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public decimal Salary { get; set; }
}

// Order-based mapping
public class OrderedPerson
{
    [Order(0)] public string Name { get; set; } = "";
    [Order(1)] public int Age { get; set; }
    [Order(2)] public decimal Salary { get; set; }
}

// Internal schema
public class InternalSchemaPerson : IHasSchema
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public decimal Salary { get; set; }

    public Dictionary<string, int> GetDictSchema() => new()
    {
        ["Name"] = 0,
        ["Age"] = 1,
        ["Salary"] = 2
    };
}

// Constructor ambiguity test cases
public class AmbiguousConstructors
{
    public string Value { get; }
    public int Number { get; }

    // Constructor 1: string
    public AmbiguousConstructors(string value)
    {
        Value = value;
        Number = -1;
    }

    // Constructor 2: int
    public AmbiguousConstructors(int number)
    {
        Value = "from-int";
        Number = number;
    }

    // Constructor 3: object (most generic)
    public AmbiguousConstructors(object obj)
    {
        Value = obj?.ToString() ?? "null";
        Number = -2;
    }
}

public class MultipleViableConstructors
{
    public string Name { get; }
    public int Age { get; }
    public string Source { get; }

    // Constructor 1: exact match for (string, int)
    public MultipleViableConstructors(string name, int age)
    {
        Name = name;
        Age = age;
        Source = "exact";
    }

    // Constructor 2: widening conversion (string, long)
    public MultipleViableConstructors(string name, long age)
    {
        Name = name;
        Age = (int)age;
        Source = "widening";
    }

    // Constructor 3: most generic (object, object)
    public MultipleViableConstructors(object name, object age)
    {
        Name = name?.ToString() ?? "";
        Age = age is int i ? i : 0;
        Source = "generic";
    }
}
public class StrictConstructorOnly
{
    public StrictConstructorOnly(int x, int y) { }
}
public class StrictConstructors
{
    public string Name { get; }
    public int Age { get; }

    public StrictConstructors(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public StrictConstructors(string name, long age)
    {
        Name = name;
        Age = (int)age;
    }

    // No generic (object, object) constructor
}

// Nullable handling
public class NullableFields
{
    public string? Name { get; set; }
    public int? Age { get; set; }
    public decimal? Salary { get; set; }
    public DateTime? HireDate { get; set; }
}

// Culture-sensitive parsing
public class CultureSensitiveData
{
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
    public DateTime Date { get; set; }
}

// No parameterless constructor
public class NoDefaultConstructor
{
    public string Name { get; }

    public NoDefaultConstructor(string name)
    {
        Name = name;
    }
}

// Private/internal members
public class PrivateMemberAccess
{
    [Order(0)] private string _internalId = "";
    [Order(1)] public string Name { get; set; } = "";

    public string GetInternalId() => _internalId;
}

// Case sensitivity test
public class CaseSensitiveModel
{
    public string Name { get; set; } = "";
    public string name { get; set; } = ""; // lowercase
    public string NAME { get; set; } = ""; // uppercase
}

// Duplicate column names in schema
public class DuplicateSchemaTest
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

// Unmapped members
public class UnmappedMembersModel
{
    public string MappedField { get; set; } = "";
    public string UnmappedField { get; set; } = "default";
    public int AnotherUnmapped { get; set; } = 42;
}

public class TrulyUnmaterializable
{
    public string Name { get; }

    // Constructor requires parameter that doesn't match schema
    public TrulyUnmaterializable(string firstName, string lastName)
    {
        Name = $"{firstName} {lastName}";
    }
}

