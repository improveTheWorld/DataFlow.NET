using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Regression tests for AllCases filtering behavior.
/// Tests that valid zero/false/empty values are NOT incorrectly filtered out.
/// </summary>
public class AllCasesFilteringTests
{
    /// <summary>
    /// BUG REPRODUCTION: AllCases incorrectly filters out 0 (zero) values
    /// when filter=true (default), because it uses Equals(default).
    /// </summary>
    [Fact]
    public void AllCases_ShouldNotFilterOutZeroValues()
    {
        // Arrange: Numbers where some transform to 0
        var numbers = new[] { 10, 0, 5 };
        
        // Act: Use Cases to categorize, SelectCase returns the number as-is
        var result = numbers
            .Cases(n => n > 5)  // >5 goes to category 0, rest to category 1
            .SelectCase(
                n => n * 2,     // Category 0: doubles (10 -> 20)
                n => n          // Category 1: as-is (0 -> 0, 5 -> 5)
            )
            .AllCases()         // Should include ALL values including 0
            .ToList();

        // Assert: 0 should NOT be filtered out
        Assert.Contains(0, result);
        Assert.Equal(3, result.Count); // 20, 0, 5
    }

    /// <summary>
    /// BUG REPRODUCTION: AllCases incorrectly filters out false values
    /// </summary>
    [Fact]
    public void AllCases_ShouldNotFilterOutFalseValues()
    {
        // Arrange
        var items = new[] { "yes", "no", "maybe" };
        
        // Act: Transform to booleans
        var result = items
            .Cases(s => s == "yes")
            .SelectCase(
                _ => true,     // "yes" -> true
                _ => false     // others -> false
            )
            .AllCases()
            .ToList();

        // Assert: false values should be included
        Assert.Contains(false, result);
        Assert.Equal(3, result.Count); // true, false, false
    }

    /// <summary>
    /// BUG REPRODUCTION: AllCases should filter out actual nulls (for reference types)
    /// </summary>
    [Fact]
    public void AllCases_ShouldFilterOutNullsForReferenceTypes()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        
        // Act: Only transform first category, others get null (implicit from supra)
        var result = items
            .Cases(n => n == 1)  // Only 1 goes to category 0
            .SelectCase(
                n => $"Value: {n}"  // Only one selector, rest get default(string) = null
            )
            .AllCases()  // Should filter nulls
            .ToList();

        // Assert: Only the transformed item, nulls filtered
        Assert.Single(result);
        Assert.Equal("Value: 1", result[0]);
    }

    /// <summary>
    /// Verify filter=false returns everything including nulls
    /// </summary>
    [Fact]
    public void AllCases_WithFilterFalse_ShouldIncludeNulls()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        
        // Act
        var result = items
            .Cases(n => n == 1)
            .SelectCase(
                n => $"Value: {n}"  // Only one selector
            )
            .AllCases(filter: false)  // Include nulls
            .ToList();

        // Assert: All 3 items including 2 nulls
        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Count(x => x == null));
    }
}
