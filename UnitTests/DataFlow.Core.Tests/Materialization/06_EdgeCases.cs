using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

public class EdgeCaseTests
{
    [Fact]
    public void Create_WithCaseInsensitiveSchema_ShouldMatchIgnoringCase()
    {
        // Arrange
        var schema = new[] { "name", "AGE", "SaLaRy" }; // Mixed case
        var values = new object[] { "John", 30, 75000m };
        schema = ObjectMaterializer.ResolveSchema<PersonMutable>(schema);
        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);
        Assert.Equal(75000m, person.Salary);
    }

    [Fact] // FIXED: NET-005 - Was case-insensitive, now auto-detects (v1.2.1)
    public void Create_WithCaseSensitiveModel_ShouldMapCorrectly()
    {
        // Arrange
        var schema = new[] { "Name", "name", "NAME" };
        var values = new object[] { "Title Case", "lower case", "UPPER CASE" };

        // Act
        var obj = ObjectMaterializer.Create<CaseSensitiveModel>(schema, values);

        // Assert
        Assert.Equal("Title Case", obj.Name);
        Assert.Equal("lower case", obj.name);
        Assert.Equal("UPPER CASE", obj.NAME);
    }

    [Fact]
    public void Create_WithPrivateMembers_ShouldSetViaReflection()
    {
        // Arrange
        var values = new object[] { "secret-id", "Public Name" };

        // Act
        var obj = ObjectMaterializer.Create<PrivateMemberAccess>(values);

        // Assert
        Assert.Equal("Public Name", obj.Name);
        Assert.Equal("secret-id", obj.GetInternalId());
    }

    [Fact]
    public void Create_WithExtraValuesInArray_ShouldIgnoreExtra()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "John", 30, 75000m, "Extra1", "Extra2" };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);
        // Extra values ignored
    }

    [Fact]
    public void Create_WithEmptySchema_ShouldCreateDefaultObject()
    {
        // Arrange
        var schema = Array.Empty<string>();
        var values = Array.Empty<object>();

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("", person.Name);
        Assert.Equal(0, person.Age);
    }

    [Fact]
    public void Create_WithWhitespaceInSchema_ShouldTrim()
    {
        // Arrange
        var schema = new[] { " Name ", " Age ", " Salary " };
        var values = new object[] { "John", 30, 75000m };
        schema = ObjectMaterializer.ResolveSchema<PersonMutable>(schema);
        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert - Should match after trimming
        Assert.Equal("John", person.Name);
        Assert.Equal(30, person.Age);
    }

    [Fact]
    public void Create_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "Jos? Garc?a ???", 30, 75000m };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.Equal("Jos? Garc?a ???", person.Name);
    }

    [Fact]
    public void Create_WithVeryLargeValues_ShouldHandle()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { new string('X', 100000), int.MaxValue, decimal.MaxValue };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.Equal(100000, person.Name.Length);
        Assert.Equal(int.MaxValue, person.Age);
        Assert.Equal(decimal.MaxValue, person.Salary);
    }

    [Fact]
    public void Create_WithDefaultValues_ShouldRespectDefaults()
    {
        // Arrange
        var schema = new[] { "Name" }; // Only set Name
        var values = new object[] { "John" };

        // Act
        var obj = ObjectMaterializer.Create<ModelWithDefaults>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal(18, obj.Age); // Default from property initializer
        Assert.Equal(50000m, obj.Salary); // Default from property initializer
    }

    [Fact]
    public void Create_WithReadOnlyProperties_ShouldSkip()
    {
        // Arrange
        var schema = new[] { "Name", "ComputedValue" };
        var values = new object[] { "John", "should-be-ignored" };

        // Act
        var obj = ObjectMaterializer.Create<ReadOnlyPropertyModel>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal("JOHN", obj.ComputedValue); // Computed, not set
    }

    [Fact]
    public void Create_WithInitOnlyProperties_ShouldSet()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "John", 30 };

        // Act
        var obj = ObjectMaterializer.Create<InitOnlyModel>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal(30, obj.Age);
    }

    [Fact]
    public void Create_ConcurrentCalls_ShouldBeCacheSafe()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var tasks = new List<Task<PersonMutable>>();

        // Act - Create 100 concurrent materializations
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var values = new object[] { $"Person{index}", 20 + index, 50000m + index };
                return ObjectMaterializer.Create<PersonMutable>(schema, values);
            }));
        }

        var results = Task.WhenAll(tasks).Result;

        // Assert - All should succeed with correct values
        Assert.Equal(100, results.Length);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal($"Person{i}", results[i].Name);
            Assert.Equal(20 + i, results[i].Age);
        }
    }
}

// Additional test models for edge cases
public class ModelWithDefaults
{
    public string Name { get; set; } = "";
    public int Age { get; set; } = 18;
    public decimal Salary { get; set; } = 50000m;
}

public class ReadOnlyPropertyModel
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set => _name = value;
    }

    public string ComputedValue => _name.ToUpper(); // Read-only
}

public class InitOnlyModel
{
    public string Name { get; init; } = "";
    public int Age { get; init; }
}
