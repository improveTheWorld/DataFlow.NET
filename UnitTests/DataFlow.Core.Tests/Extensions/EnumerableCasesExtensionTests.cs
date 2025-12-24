using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Comprehensive tests for EnumerableCasesExtension - all overloads.
/// Target: Increase coverage from 10% to 50%+
/// </summary>
public class EnumerableCasesExtensionTests
{
    #region Cases with Category Enum

    [Fact]
    public void Cases_WithCategoryEnum_MapsCorrectly()
    {
        // Arrange - items already have category tags
        var items = new[]
        {
            ("A", "apple"),
            ("B", "banana"),
            ("A", "avocado"),
            ("C", "cherry")
        };

        // Act - map category strings to indices
        var result = items.Cases("A", "B", "C").ToList();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal((0, "apple"), result[0]);   // A -> 0
        Assert.Equal((1, "banana"), result[1]);  // B -> 1
        Assert.Equal((0, "avocado"), result[2]); // A -> 0
        Assert.Equal((2, "cherry"), result[3]);  // C -> 2
    }

    [Fact]
    public void Cases_UnknownCategory_MapsToCatchAll()
    {
        // Arrange
        var items = new[]
        {
            ("known", "item1"),
            ("unknown", "item2")
        };

        // Act - only "known" is mapped
        var result = items.Cases("known").ToList();

        // Assert
        Assert.Equal((0, "item1"), result[0]);  // known -> 0
        Assert.Equal((1, "item2"), result[1]);  // unknown -> catchall (1)
    }

    #endregion

    #region SelectCase with Index

    [Fact]
    public void SelectCase_WithIndex_PassesIndexToSelector()
    {
        // Arrange
        var items = new[] { "a", "b", "c" }
            .Cases(s => s == "a", s => s == "b");

        // Act - use indexed selector
        var result = items
            .SelectCase(
                (s, idx) => $"a-{idx}",
                (s, idx) => $"b-{idx}",
                (s, idx) => $"c-{idx}"
            )
            .ToList();

        // Assert
        Assert.Equal("a-0", result[0].item);
        Assert.Equal("b-1", result[1].item);
        Assert.Equal("c-2", result[2].item);
    }

    [Fact]
    public void SelectCase_ChainedTransform_AppliesSequentially()
    {
        // Arrange
        var items = new[] { 1, 2, 3 }
            .Cases(n => n == 1, n => n == 2);

        // Act - first transform
        var firstTransform = items.SelectCase<int, int>(
            n => n * 10,
            n => n * 20,
            n => n * 30
        );

        // Second transform on newItem
        var result = firstTransform.SelectCase<int, int, string>(
            n => $"one:{n}",
            n => $"two:{n}",
            n => $"other:{n}"
        ).ToList();

        // Assert
        Assert.Equal("one:10", result[0].newItem);
        Assert.Equal("two:40", result[1].newItem);
        Assert.Equal("other:90", result[2].newItem);
    }

    [Fact]
    public void SelectCase_CategoryExceedsSelectorCount_ReturnsDefault()
    {
        // Arrange - 3 categories, but only 2 selectors
        var items = new[] { "a", "b", "c" }
            .Cases(s => s == "a", s => s == "b"); // c goes to category 2

        // Act - only 2 selectors provided
        var result = items
            .SelectCase<string, string>(
                s => "matched-a",
                s => "matched-b"
            )
            .ToList();

        // Assert
        Assert.Equal("matched-a", result[0].newItem);
        Assert.Equal("matched-b", result[1].newItem);
        Assert.Null(result[2].newItem); // category 2 has no selector -> default
    }

    #endregion

    #region ForEachCase

    [Fact]
    public void ForEachCase_WithParameterlessAction_ExecutesPerCategory()
    {
        // Arrange
        var counts = new int[3];
        var items = new[] { "a", "b", "a", "c" }
            .Cases(s => s == "a", s => s == "b");

        // Act
        items.ForEachCase(
            () => counts[0]++,
            () => counts[1]++,
            () => counts[2]++
        ).ToList(); // Force evaluation

        // Assert
        Assert.Equal(2, counts[0]); // "a" twice
        Assert.Equal(1, counts[1]); // "b" once
        Assert.Equal(1, counts[2]); // "c" once (supra)
    }

    [Fact]
    public void ForEachCase_WithItemAction_ReceivesItem()
    {
        // Arrange
        var collected = new List<string>();
        var items = new[] { "error", "info", "error" }
            .Cases(s => s == "error");

        // Act
        items.ForEachCase(
            s => collected.Add($"ERR:{s}"),
            s => collected.Add($"OTHER:{s}")
        ).ToList();

        // Assert
        Assert.Contains("ERR:error", collected);
        Assert.Contains("OTHER:info", collected);
        Assert.Equal(3, collected.Count);
    }

    [Fact]
    public void ForEachCase_WithIndexedAction_ReceivesIndex()
    {
        // Arrange
        var logs = new List<string>();
        var items = new[] { "x", "y", "z" }
            .Cases(s => true); // all match category 0

        // Act
        items.ForEachCase(
            (s, idx) => logs.Add($"{s}@{idx}")
        ).ToList();

        // Assert
        Assert.Equal(new[] { "x@0", "y@1", "z@2" }, logs);
    }

    [Fact]
    public void ForEachCase_WithNewItem_ActionsReceiveNewItem()
    {
        // Arrange
        var processed = new List<int>();
        var items = new[] { "a", "b" }
            .Cases(s => s == "a")
            .SelectCase(s => 10, s => 20);

        // Act
        items.ForEachCase(
            n => processed.Add(n),
            n => processed.Add(n)
        ).ToList();

        // Assert
        Assert.Equal(new[] { 10, 20 }, processed);
    }

    #endregion

    #region UnCase

    [Fact]
    public void UnCase_FromCategorized_ReturnsOriginalItems()
    {
        // Arrange
        var items = new[] { "apple", "banana", "cherry" }
            .Cases(s => s.StartsWith("a"));

        // Act
        var result = items.UnCase().ToList();

        // Assert
        Assert.Equal(new[] { "apple", "banana", "cherry" }, result);
    }

    [Fact]
    public void UnCase_FromTransformed_ReturnsOriginalItems()
    {
        // Arrange
        var items = new[] { 1, 2, 3 }
            .Cases(n => n > 1)
            .SelectCase(n => n * 10, n => n * 20);

        // Act - UnCase should return original items, not transformed
        var result = items.UnCase().ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion

    #region AllCases with filter option

    [Fact]
    public void AllCases_WithFilterFalse_IncludesDefaults()
    {
        // Arrange - category 2 has no selector -> null
        var items = new[] { "a", "b", "c" }
            .Cases(s => s == "a", s => s == "b")
            .SelectCase<string, string>(
                s => "A",
                s => "B"
                // No selector for category 2
            );

        // Act - filter=false includes default values
        var result = items.AllCases(filter: false).ToList();

        // Assert - includes null for "c"
        Assert.Equal(3, result.Count);
        Assert.Contains((string?)null, result);
    }

    [Fact]
    public void AllCases_WithFilterTrue_ExcludesDefaults()
    {
        // Arrange
        var items = new[] { "a", "b", "c" }
            .Cases(s => s == "a", s => s == "b")
            .SelectCase<string, string>(
                s => "A",
                s => "B"
            );

        // Act - default filter=true
        var result = items.AllCases().ToList();

        // Assert - excludes null for "c"
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain((string?)null, result);
    }

    #endregion
}
