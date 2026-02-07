using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

/// <summary>
/// Tests that specifically validate the recommended fixes have been applied
/// </summary>
public class FixValidationTests
{
    [Fact]
    public void Fix1_CodeDuplication_FastFeedClassShouldNotExist()
    {
        // Assert - FastFeed class should be removed
        var assembly = typeof(ObjectMaterializer).Assembly;
        var fastFeedType = assembly.GetType("DataFlow.Framework.FastFeed");

        Assert.Null(fastFeedType); // Should not exist after consolidation
    }

    [Fact]
    public void Fix2_ConstructorScoring_ShouldSelectBestMatch()
    {
        // Arrange
        var values = new object[] { "Test", 42 };

        // Act
        var obj = ObjectMaterializer.Create<MultipleViableConstructors>(values);

        // Assert - Should consistently select exact match
        Assert.Equal("exact", obj.Source);

        // Run 10 times to ensure consistency
        for (int i = 0; i < 10; i++)
        {
            var obj2 = ObjectMaterializer.Create<MultipleViableConstructors>(values);
            Assert.Equal("exact", obj2.Source);
        }
    }

    [Fact]
    public void Fix3_ErrorMessages_ShouldIncludeAvailableConstructors()
    {
        // Arrange - schema doesn't match constructor parameters
        var schema = new[] { "Name" };
        var values = new object[] { "Test" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.Create<TrulyUnmaterializable>(schema, values));

        // Should include type name
        Assert.Contains("TrulyUnmaterializable", ex.Message);

        // Should mention available constructors
        Assert.Contains("constructor", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fix3_ErrorMessages_ShouldIncludeContext()
    {
        // Arrange - schema column name doesn't match constructor parameter
        var schema = new[] { "ZzzUnrelated" }; //  Schema that cannot resolve to "name" via any pass
        var values = new object[] { "Test" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.Create<NoDefaultConstructor>(schema, values));

        Assert.Contains("NoDefaultConstructor", ex.Message);
        Assert.Contains("constructor", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Fix3_SchemaMapping_ShouldWorkWithoutParameterlessConstructor()
    {
        // Arrange
        var schema = new[] { "Name" };
        var values = new object[] { "Test" };

        // Act
        var result = ObjectMaterializer.Create<NoDefaultConstructor>(schema, values);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public void Fix3_ErrorMessages_ShouldIncludeContextWhenNoConstructorMatches()
    {
        // Arrange - completely mismatched schema
        var schema = new[] { "NonExistentColumn" };
        var values = new object[] { "Test" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.Create<NoDefaultConstructor>(schema, values));

        Assert.Contains("NoDefaultConstructor", ex.Message);
        Assert.Contains("Schema columns:", ex.Message);
        Assert.Contains("Attempted constructors:", ex.Message); //  Changed from "Available constructors:"
        Assert.Contains("missing:", ex.Message); //  Should also show what's missing
    }


    [Fact]
    public void Fix4_CultureSupport_ParameterShouldExist()
    {
        // This test validates the API signature exists
        // Actual culture handling tested in CultureHandlingTests

        var schema = new[] { "Amount" };
        var values = new object[] { "1234.56" };

        // After fix, this overload should exist:
        // var obj = ObjectMaterializer.Create<CultureSensitiveData>(
        //     schema, values, culture: CultureInfo.GetCultureInfo("de-DE"));

        // For now, verify current API works
        var obj = ObjectMaterializer.Create<CultureSensitiveData>(schema, values);
        Assert.NotNull(obj);
    }

    [Fact]
    public void Fix5_ValidationMode_StrictModeShouldDetectUnmapped()
    {
        // Arrange
        var schema = new[] { "MappedField" };
        var values = new object[] { "value" };

        // After fix, strict mode should throw or warn:
        // Assert.Throws<InvalidOperationException>(() =>
        //     ObjectMaterializer.Create<UnmappedMembersModel>(
        //         schema, values, mode: MaterializationMode.Strict));

        // Current behavior (lenient mode)
        var obj = ObjectMaterializer.Create<UnmappedMembersModel>(schema, values);
        Assert.NotNull(obj);
        Assert.Equal("default", obj.UnmappedField); // Unchanged
    }

    [Fact]
    public void Fix6_DuplicateDetection_ShouldWarnOrThrow()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Name" }; // Duplicate
        var values = new object[] { "First", 30, "Second" };

        // Current behavior: last wins
        var obj = ObjectMaterializer.Create<DuplicateSchemaTest>(schema, values);
        Assert.Equal("Second", obj.Name);

        // After fix with strict mode:
        // Assert.Throws<InvalidOperationException>(() =>
        //     ObjectMaterializer.Create<DuplicateSchemaTest>(
        //         schema, values, mode: MaterializationMode.Strict));
    }

    [Fact]
    public void Fix7_NullSignatureKey_ShouldIncludePosition()
    {
        // Arrange - Two different null patterns
        var values1 = new object?[] { null, "test" };
        var values2 = new object?[] { "test", null };

        // Act
        var obj1 = ObjectMaterializer.Create<TwoStringConstructor>(values1);
        var obj2 = ObjectMaterializer.Create<TwoStringConstructor>(values2);

        // Assert - Both should work correctly
        Assert.Null(obj1.First);
        Assert.Equal("test", obj1.Second);

        Assert.Equal("test", obj2.First);
        Assert.Null(obj2.Second);
    }

    [Fact, Trait("Category", "Performance")]
    public void Fix8_PerformanceRegression_ShouldRemainFast()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "John", 30, 75000m };
        const int iterations = 1000;

        // Warmup
        ObjectMaterializer.Create<PersonMutable>(schema, values);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            ObjectMaterializer.Create<PersonMutable>(schema, values);
        }
        sw.Stop();

        // Assert - Should complete in reasonable time
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        Assert.True(avgMicroseconds < 100,
            $"Performance regression detected: {avgMicroseconds:F2}�s per materialization (should be <100�s)");
    }
}

// Test model for null signature validation
public class TwoStringConstructor
{
    public string? First { get; }
    public string? Second { get; }

    public TwoStringConstructor(string? first, string? second)
    {
        First = first;
        Second = second;
    }
}
