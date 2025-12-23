using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

public class BasicMaterializationTests
{
    [Fact]
    public void Create_WithSchemaAndValues_ShouldMaterializeCorrectly()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "John Doe", 30, 75000.50m };

        // Act
        var person = ObjectMaterializer.Create<Person>(schema, values);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("John Doe", person.Name);
        Assert.Equal(30, person.Age);
        Assert.Equal(75000.50m, person.Salary);
    }

    [Fact]
    public void Create_WithMutableClass_ShouldSetProperties()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "Jane Smith", 28, 82000m };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("Jane Smith", person.Name);
        Assert.Equal(28, person.Age);
        Assert.Equal(82000m, person.Salary);
    }

    [Fact]
    public void Create_WithOrderAttribute_ShouldMapByPosition()
    {
        // Arrange
        var values = new object[] { "Bob Johnson", 35, 90000m };

        // Act
        var person = ObjectMaterializer.Create<OrderedPerson>(values);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("Bob Johnson", person.Name);
        Assert.Equal(35, person.Age);
        Assert.Equal(90000m, person.Salary);
    }

    [Fact]
    public void Create_WithInternalSchema_ShouldUseProvidedMapping()
    {
        // Arrange
        var values = new object[] { "Alice Brown", 32, 88000m };

        // Act
        var person = ObjectMaterializer.Create<InternalSchemaPerson>(values);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("Alice Brown", person.Name);
        Assert.Equal(32, person.Age);
        Assert.Equal(88000m, person.Salary);
    }

    [Fact]
    public void Create_WithEmptyValues_ShouldCreateDefaultInstance()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = Array.Empty<object>();

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("", person.Name);
        Assert.Equal(0, person.Age);
        Assert.Equal(0m, person.Salary);
    }

    [Fact]
    public void Create_WithPartialValues_ShouldFillAvailableFields()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "Partial Person", 25 };

        // Act
        var person = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("Partial Person", person.Name);
        Assert.Equal(25, person.Age);
        Assert.Equal(0m, person.Salary); // Unmapped field gets default
    }
}
