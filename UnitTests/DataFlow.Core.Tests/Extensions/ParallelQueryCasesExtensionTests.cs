using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for ParallelQueryCasesExtension - parallel LINQ Cases pattern.
/// </summary>
public class ParallelQueryCasesExtensionTests
{
    #region Cases

    [Fact]
    public void Cases_WithFilters_CategorizesByFirstMatch()
    {
        // Arrange
        var items = new[] { "error", "warn", "info", "error" }.AsParallel();

        // Act
        var result = items.Cases(s => s == "error", s => s == "warn").ToList();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains(result, r => r.category == 0 && r.item == "error");
        Assert.Contains(result, r => r.category == 1 && r.item == "warn");
        Assert.Contains(result, r => r.category == 2 && r.item == "info"); // catch-all
    }

    [Fact]
    public void Cases_WithCategoryEnum_MapsCorrectly()
    {
        // Arrange
        var items = new[] { ("A", 1), ("B", 2), ("A", 3), ("C", 4) }.AsParallel();

        // Act
        var result = items.Cases("A", "B", "C").ToList();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains(result, r => r.categoryIndex == 0 && r.item == 1);
        Assert.Contains(result, r => r.categoryIndex == 1 && r.item == 2);
        Assert.Contains(result, r => r.categoryIndex == 2 && r.item == 4);
    }

    [Fact]
    public void Cases_UnknownCategory_MapsToCatchAll()
    {
        // Arrange
        var items = new[] { ("known", "item1"), ("unknown", "item2") }.AsParallel();

        // Act
        var result = items.Cases("known").ToList();

        // Assert
        Assert.Contains(result, r => r.categoryIndex == 0 && r.item == "item1");
        Assert.Contains(result, r => r.categoryIndex == 1 && r.item == "item2"); // catchall
    }

    #endregion

    #region SelectCase

    [Fact]
    public void SelectCase_TransformsPerCategory()
    {
        // Arrange
        var items = new[] { 1, 2, 3 }
            .AsParallel()
            .Cases(n => n == 1, n => n == 2);

        // Act
        var result = items.SelectCase<int, int>(
            n => n * 10,
            n => n * 20,
            n => n * 30
        ).ToList();

        // Assert
        Assert.Contains(result, r => r.newItem == 10);  // 1 * 10
        Assert.Contains(result, r => r.newItem == 40);  // 2 * 20
        Assert.Contains(result, r => r.newItem == 90);  // 3 * 30
    }

    [Fact]
    public void SelectCase_CategoryExceedsSelectors_ReturnsDefault()
    {
        // Arrange
        var items = new[] { "a", "b", "c" }
            .AsParallel()
            .Cases(s => s == "a", s => s == "b");

        // Act - only 2 selectors
        var result = items.SelectCase<string, string>(
            s => "matched-a",
            s => "matched-b"
        ).ToList();

        // Assert
        Assert.Contains(result, r => r.newItem == "matched-a");
        Assert.Contains(result, r => r.newItem == "matched-b");
        Assert.Contains(result, r => r.newItem == null); // default for "c"
    }

    [Fact]
    public void SelectCase_WithNewItem_ChainsCorrectly()
    {
        // Arrange
        var items = new[] { 1, 2 }
            .AsParallel()
            .Cases(n => n == 1)
            .SelectCase<int, int>(n => n * 10, n => n * 20);

        // Act - chain another SelectCase
        var result = items.SelectCase<int, int, string>(
            n => $"ten:{n}",
            n => $"twenty:{n}"
        ).ToList();

        // Assert
        Assert.Contains(result, r => r.newItem == "ten:10");
        Assert.Contains(result, r => r.newItem == "twenty:40");
    }

    #endregion

    #region ForEachCase

    [Fact]
    public void ForEachCase_ExecutesPerCategory()
    {
        // Arrange
        var counts = new int[3];
        var items = new[] { "a", "b", "a", "c" }
            .AsParallel()
            .Cases(s => s == "a", s => s == "b");

        // Act
        items.ForEachCase(
            () => Interlocked.Increment(ref counts[0]),
            () => Interlocked.Increment(ref counts[1]),
            () => Interlocked.Increment(ref counts[2])
        ).ToList();

        // Assert
        Assert.Equal(2, counts[0]); // "a" twice
        Assert.Equal(1, counts[1]); // "b" once
        Assert.Equal(1, counts[2]); // "c" once
    }

    [Fact]
    public void ForEachCase_WithItem_ReceivesItem()
    {
        // Arrange
        var collected = new System.Collections.Concurrent.ConcurrentBag<string>();
        var items = new[] { "x", "y" }
            .AsParallel()
            .Cases(s => s == "x");

        // Act
        items.ForEachCase(
            s => collected.Add($"X:{s}"),
            s => collected.Add($"other:{s}")
        ).ToList();

        // Assert
        Assert.Contains("X:x", collected);
        Assert.Contains("other:y", collected);
    }

    #endregion

    #region UnCase

    [Fact]
    public void UnCase_ReturnsOriginalItems()
    {
        // Arrange
        var items = new[] { "apple", "banana", "cherry" }
            .AsParallel()
            .Cases(s => s.StartsWith("a"));

        // Act
        var result = items.UnCase().ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("apple", result);
        Assert.Contains("banana", result);
        Assert.Contains("cherry", result);
    }

    [Fact]
    public void UnCase_FromTransformed_ReturnsOriginalItems()
    {
        // Arrange
        var items = new[] { 1, 2, 3 }
            .AsParallel()
            .Cases(n => n > 1)
            .SelectCase<int, int>(n => n * 10, n => n * 20);

        // Act
        var result = items.UnCase().ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
    }

    #endregion

    #region AllCases

    [Fact]
    public void AllCases_WithFilterTrue_ExcludesDefaults()
    {
        // Arrange
        var items = new[] { "a", "b", "c" }
            .AsParallel()
            .Cases(s => s == "a", s => s == "b")
            .SelectCase<string, string>(
                s => "A",
                s => "B"
            );

        // Act
        var result = items.AllCases(filter: true).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
    }

    [Fact]
    public void AllCases_WithFilterFalse_IncludesDefaults()
    {
        // Arrange
        var items = new[] { "a", "b", "c" }
            .AsParallel()
            .Cases(s => s == "a", s => s == "b")
            .SelectCase<string, string>(
                s => "A",
                s => "B"
            );

        // Act
        var result = items.AllCases(filter: false).ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    #endregion
}
