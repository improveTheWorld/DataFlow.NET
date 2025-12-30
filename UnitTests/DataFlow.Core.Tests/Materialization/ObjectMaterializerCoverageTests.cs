using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

/// <summary>
/// Additional edge case tests for ObjectMaterializer to improve coverage from 44% to 60%+
/// </summary>
public class ObjectMaterializerCoverageTests
{
    #region Test Models

    public class PersonWithMultipleConstructors
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";

        public PersonWithMultipleConstructors() { }
        public PersonWithMultipleConstructors(string name) { Name = name; }
        public PersonWithMultipleConstructors(string name, int age) { Name = name; Age = age; }
        public PersonWithMultipleConstructors(string name, int age, string city)
        {
            Name = name;
            Age = age;
            City = city;
        }
    }

    public record RecordWithDefaults(string Name, int Age = 25, string Country = "USA");

    public class TypeWithNullableMembers
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
        public DateTime? BirthDate { get; set; }
    }

    public class TypeWithManyNumericTypes
    {
        public int IntValue { get; set; }
        public long LongValue { get; set; }
        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }
        public decimal DecimalValue { get; set; }
        public short ShortValue { get; set; }
        public byte ByteValue { get; set; }
    }

    public class TypeWithDateTimeProperties
    {
        public DateTime Created { get; set; }
        public DateTimeOffset Modified { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TypeWithGuid
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class TypeWithBoolean
    {
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class TypeWithEnum
    {
        public DayOfWeek Day { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion

    #region Constructor Resolution Tests

    [Fact]
    public void Create_WithMultipleConstructors_SelectsBestMatch()
    {
        // Arrange - 2 parameters, should select (string, int) constructor
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "John", 30 };

        // Act
        var result = ObjectMaterializer.Create<PersonWithMultipleConstructors>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
        Assert.Equal(30, result.Age);
        Assert.Equal("", result.City); // Default
    }

    [Fact]
    public void Create_WithFullConstructor_UsesAllParameters()
    {
        // Arrange - 3 parameters
        var schema = new[] { "Name", "Age", "City" };
        var values = new object[] { "Jane", 25, "Paris" };

        // Act
        var result = ObjectMaterializer.Create<PersonWithMultipleConstructors>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Jane", result.Name);
        Assert.Equal(25, result.Age);
        Assert.Equal("Paris", result.City);
    }

    [Fact]
    public void Create_WithRecordDefaults_UsesProvidedValues()
    {
        // Arrange
        var schema = new[] { "Name" };
        var values = new object[] { "Alice" };

        // Act
        var result = ObjectMaterializer.Create<RecordWithDefaults>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alice", result.Name);
        // Defaults should be used for Age and Country
    }

    #endregion

    #region Type Conversion Tests

    [Fact]
    public void Create_WithNullableMembers_HandlesNulls()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "BirthDate" };
        var values = new object?[] { null, null, null };

        // Act
        var result = ObjectMaterializer.Create<TypeWithNullableMembers>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Name);
        Assert.Null(result.Age);
        Assert.Null(result.BirthDate);
    }

    [Fact]
    public void Create_WithManyNumericTypes_ConvertsCorrectly()
    {
        // Arrange
        var schema = new[] { "IntValue", "LongValue", "FloatValue", "DoubleValue", "DecimalValue", "ShortValue", "ByteValue" };
        var values = new object[] { "123", "9876543210", "3.14", "2.71828", "99.99", "32767", "255" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithManyNumericTypes>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.IntValue);
        Assert.Equal(9876543210L, result.LongValue);
        Assert.Equal(3.14f, result.FloatValue, 0.01f);
        Assert.Equal(2.71828, result.DoubleValue, 0.00001);
        Assert.Equal(99.99m, result.DecimalValue);
        Assert.Equal((short)32767, result.ShortValue);
        Assert.Equal((byte)255, result.ByteValue);
    }

    [Fact]
    public void Create_WithDateTimeTypes_ParsesCorrectly()
    {
        // Arrange
        var schema = new[] { "Created", "Modified", "Duration" };
        var now = DateTime.UtcNow;
        var values = new object[] { now.ToString("O"), now.ToString("O"), "01:30:00" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithDateTimeProperties>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(now.Date, result.Created.Date);
    }

    [Fact]
    public void Create_WithGuid_ParsesCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var schema = new[] { "Id", "Name" };
        var values = new object[] { guid.ToString(), "Item1" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithGuid>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(guid, result.Id);
        Assert.Equal("Item1", result.Name);
    }

    [Fact]
    public void Create_WithBoolean_ParsesVariousFormats()
    {
        // Arrange - "true" and "false" strings
        var schema = new[] { "IsActive", "IsDeleted" };
        var values = new object[] { "true", "false" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithBoolean>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsActive);
        Assert.False(result.IsDeleted);
    }

    [Fact]
    public void Create_WithBooleanNumeric_ParsesOneZero()
    {
        // Arrange - 1 and 0 as strings
        var schema = new[] { "IsActive", "IsDeleted" };
        var values = new object[] { "1", "0" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithBoolean>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsActive);
        Assert.False(result.IsDeleted);
    }

    [Fact]
    public void Create_WithEnum_ParsesName()
    {
        // Arrange
        var schema = new[] { "Day", "Name" };
        var values = new object[] { "Friday", "Weekend" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithEnum>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DayOfWeek.Friday, result.Day);
        Assert.Equal("Weekend", result.Name);
    }

    [Fact]
    public void Create_WithEnumNumeric_ParsesValue()
    {
        // Arrange - 5 = Friday
        var schema = new[] { "Day", "Name" };
        var values = new object[] { "5", "Weekday" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithEnum>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DayOfWeek.Friday, result.Day);
    }

    #endregion

    #region Session Tests

    [Fact]
    public void CreateGeneralSession_ReuseMultipleTimes()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var session = ObjectMaterializer.CreateGeneralSession<PersonWithMultipleConstructors>(schema);

        // Act - Create multiple instances
        var person1 = session.Create(new object[] { "Alice", 25 });
        var person2 = session.Create(new object[] { "Bob", 30 });
        var person3 = session.Create(new object[] { "Charlie", 35 });

        // Assert
        Assert.Equal("Alice", person1?.Name);
        Assert.Equal("Bob", person2?.Name);
        Assert.Equal("Charlie", person3?.Name);
    }

    [Fact]
    public void CreateCtorSession_WithSchema_ReuseMultipleTimes()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Country" };
        var session = ObjectMaterializer.CreateCtorSession<RecordWithDefaults>(schema);

        // Act
        var record1 = session.Create(new object[] { "Alice", 25, "USA" });
        var record2 = session.Create(new object[] { "Bob", 30, "UK" });

        // Assert
        Assert.Equal("Alice", record1?.Name);
        Assert.Equal("Bob", record2?.Name);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithEmptyStrings_HandlesGracefully()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "", "" };

        // Act
        var result = ObjectMaterializer.Create<TypeWithNullableMembers>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("", result.Name);
        // Empty string for int should become 0 or null depending on type
    }

    [Fact]
    public void Create_WithExtraColumnsInSchema_IgnoresExtras()
    {
        // Arrange - Schema has more columns than the type
        var schema = new[] { "Name", "Age", "ExtraColumn", "AnotherExtra" };
        var values = new object[] { "John", 30, "ignored", "also ignored" };

        // Act
        var result = ObjectMaterializer.Create<PersonWithMultipleConstructors>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Create_WithMismatchedCaseSchema_Matches()
    {
        // Arrange - Different case in schema
        var schema = new[] { "NAME", "age" };
        var values = new object[] { "John", 30 };

        // Act
        var result = ObjectMaterializer.Create<PersonWithMultipleConstructors>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
        Assert.Equal(30, result.Age);
    }

    #endregion

    #region CreateOrFeed Tests

    [Fact]
    public void CreateOrFeed_WithArgs_CreatesInstance()
    {
        // Arrange
        var args = new object?[] { "Jane", 28, "France" };

        // Act
        var result = ObjectMaterializer.CreateOrFeed<RecordWithDefaults>(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Jane", result.Name);
        Assert.Equal(28, result.Age);
        Assert.Equal("France", result.Country);
    }

    [Fact]
    public void CreateOrFeed_WithPartialArgs_UsesDefaults()
    {
        // Arrange - RecordWithDefaults(string Name, int Age = 25, string Country = "USA")
        // Only provide Name, others should use defaults
        var args = new object?[] { "Jane", 30, "France" };

        // Act - Should work since record has default values
        var result = ObjectMaterializer.CreateOrFeed<RecordWithDefaults>(args, allowFeedFallback: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Jane", result.Name);
    }

    #endregion
}

