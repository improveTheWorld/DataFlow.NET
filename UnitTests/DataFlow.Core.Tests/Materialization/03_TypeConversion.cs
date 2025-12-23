using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

public class TypeConversionTests
{
    [Fact]
    public void Create_WithStringToIntConversion_ShouldParse()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "John", "30", "75000.50" }; // All strings

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.Equal(30, person.Age);
        Assert.Equal(75000.50m, person.Salary);
    }

    [Fact]
    public void Create_WithNullableFields_ShouldHandleNulls()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary", "HireDate" };
        var values = new object?[] { null, null, null, null };

        // Act
        var obj = ObjectMaterializer.Create<NullableFields>(schema, values);

        // Assert
        Assert.NotNull(obj);
        Assert.Null(obj.Name);
        Assert.Null(obj.Age);
        Assert.Null(obj.Salary);
        Assert.Null(obj.HireDate);
    }

    [Fact]
    public void Create_WithNullableFields_ShouldHandleValues()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary", "HireDate" };
        var values = new object[] { "Jane", "28", "82000", "2020-01-15" };

        // Act
        var obj = ObjectMaterializer.Create<NullableFields>(schema, values);

        // Assert
        Assert.Equal("Jane", obj.Name);
        Assert.Equal(28, obj.Age);
        Assert.Equal(82000m, obj.Salary);
        Assert.Equal(new DateTime(2020, 1, 15), obj.HireDate);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void ConvertObject_WithBooleanStrings_ShouldParse(string input, bool expected)
    {
        // Arrange
        var schema = new[] { "Value" };
        var values = new object[] { input };

        // Act
        var obj = ObjectMaterializer.Create<BooleanModel>(schema, values);

        // Assert
        Assert.Equal(expected, obj.Value);
    }

    [Fact]
    public void Create_WithGuidString_ShouldParse()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var schema = new[] { "Id" };
        var values = new object[] { guid.ToString() };

        // Act
        var obj = ObjectMaterializer.Create<GuidModel>(schema, values);

        // Assert
        Assert.Equal(guid, obj.Id);
    }

    [Fact]
    public void Create_WithInvalidTypeConversion_ShouldThrow()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { "not-a-number" };

        // Act & Assert
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.Create<PersonMutable>(schema, values));

        // Verify the inner exception is FormatException
        Assert.IsType<FormatException>(ex.InnerException);
        Assert.Contains("not-a-number", ex.InnerException.Message);
    }


    [Fact]
    public void Create_WithNumericWidening_ShouldConvert()
    {
        // Arrange
        var schema = new[] { "Amount", "Percentage" };
        var values = new object[] { 100, 50 }; // int -> decimal, int -> double

        // Act
        var obj = ObjectMaterializer.Create<CultureSensitiveData>(schema, values);

        // Assert
        Assert.Equal(100m, obj.Amount);
        Assert.Equal(50.0, obj.Percentage);
    }
}

// Additional test models
public class BooleanModel
{
    public bool Value { get; set; }
}

public class GuidModel
{
    public Guid Id { get; set; }
}
