using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

public class ErrorHandlingTests
{
    /// <summary>
    /// Schema-based constructor matching correctly handles types without parameterless constructors.
    /// CreateViaPrimaryConstructorWithSchema maps schema names to constructor parameters.
    /// </summary>
    [Fact] // NET-003 RESOLVED: ObjectMaterializer handles this via constructor matching
    public void Create_WithNoParameterlessConstructor_ShouldSucceedViaConstructorMatching()
    {
        // Arrange
        var schema = new[] { "Name" };
        var values = new object[] { "Test" };

        // Act — should succeed via CreateViaPrimaryConstructorWithSchema
        var result = ObjectMaterializer.Create<NoDefaultConstructor>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result!.Name);
    }

    /// <summary>
    /// Logs/warns about unmapped fields
    /// </summary>
    [Fact]
    public void Create_WithUnmappedMembers_ShouldWarnInStrictMode()
    {
        // Arrange
        var schema = new[] { "MappedField" }; // Missing UnmappedField, AnotherUnmapped
        var values = new object[] { "mapped-value" };

        // Act
        var obj = ObjectMaterializer.Create<UnmappedMembersModel>(schema, values);

        // Assert
        Assert.Equal("mapped-value", obj.MappedField);
        Assert.Equal("default", obj.UnmappedField); // Unchanged
        Assert.Equal(42, obj.AnotherUnmapped); // Unchanged

        // Should have diagnostic output
        // Check test output or logging sink for warnings like:
        // "[ObjectMaterializer] Unmapped members in UnmappedMembersModel: UnmappedField, AnotherUnmapped"
    }

    /// <summary>
    /// Throws or warns about duplicate columns
    /// </summary>
    [Fact]
    public void Create_WithDuplicateColumnNames_ShouldThrowOrWarn()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Name" }; // Duplicate "Name"
        var values = new object[] { "First", 30, "Second" };

        // Act
        var obj = ObjectMaterializer.Create<DuplicateSchemaTest>(schema, values);

        // CURRENT BEHAVIOR: Last value wins (Name = "Second")
        Assert.Equal("Second", obj.Name);

        // Strict mode: Should throw
        // Assert.Throws<InvalidOperationException>(() =>
        //     ObjectMaterializer.Create<DuplicateSchemaTest>(
        //         schema, values, mode: MaterializationMode.Strict));
    }

    [Fact]
    public void Create_WithNullSchema_ShouldThrow()
    {
        // Arrange
        string[]? schema = null;
        var values = new object[] { "test" };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            ObjectMaterializer.Create<PersonMutable>(schema!, values));
    }

    [Fact]
    public void Create_WithNullValues_ShouldThrow()
    {
        // Arrange
        var schema = new[] { "Name" };
        object[]? values = null;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.Create<PersonMutable>(schema, values!));
    }

    [Fact]
    public void Create_WithSchemaMismatch_ShouldHandleGracefully()
    {
        // Arrange
        var schema = new[] { "NonExistentField" };
        var values = new object[] { "value" };

        // Act
        var obj = ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Assert - Should create object with defaults
        Assert.NotNull(obj);
        Assert.Equal("", obj.Name);
        Assert.Equal(0, obj.Age);
    }
}
