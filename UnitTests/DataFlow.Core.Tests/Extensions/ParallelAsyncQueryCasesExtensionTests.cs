using DataFlow.Extensions;
using DataFlow.Parallel;
using DataFlow.Framework;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for ParallelAsyncQueryCasesExtension - parallel async Cases pattern.
/// </summary>
public class ParallelAsyncQueryCasesExtensionTests
{
    #region Test Helpers

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static ParallelAsyncQuery<T> ToParallelAsync<T>(IEnumerable<T> items)
    {
        return ToAsyncEnumerable(items).AsParallel();
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
            result.Add(item);
        return result;
    }

    #endregion

    #region Cases

    [Fact]
    public async Task Cases_WithFilters_CategorizesByFirstMatch()
    {
        // Arrange
        var items = ToParallelAsync(new[] { "error", "warn", "info", "error" });

        // Act
        var result = await CollectAsync(
            items.Cases(s => s == "error", s => s == "warn")
        );

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains(result, r => r.category == 0 && r.item == "error");
        Assert.Contains(result, r => r.category == 1 && r.item == "warn");
        Assert.Contains(result, r => r.category == 2 && r.item == "info");
    }

    [Fact]
    public async Task Cases_WithCategoryEnum_MapsCorrectly()
    {
        // Arrange
        var items = ToParallelAsync(new[] { ("A", 1), ("B", 2), ("A", 3) });

        // Act
        var result = await CollectAsync(items.Cases("A", "B"));

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.categoryIndex == 0);
        Assert.Contains(result, r => r.categoryIndex == 1);
    }

    #endregion

    #region SelectCase

    [Fact]
    public async Task SelectCase_TransformsPerCategory()
    {
        // Arrange
        var items = ToParallelAsync(new[] { 1, 2, 3 })
            .Cases(n => n == 1, n => n == 2);

        // Act
        var result = await CollectAsync(
            items.SelectCase<int, int>(
                n => n * 10,
                n => n * 20,
                n => n * 30
            )
        );

        // Assert
        Assert.Contains(result, r => r.newItem == 10);
        Assert.Contains(result, r => r.newItem == 40);
        Assert.Contains(result, r => r.newItem == 90);
    }

    [Fact]
    public async Task SelectCase_CategoryExceedsSelectors_ReturnsDefault()
    {
        // Arrange
        var items = ToParallelAsync(new[] { "a", "b", "c" })
            .Cases(s => s == "a", s => s == "b");

        // Act
        var result = await CollectAsync(
            items.SelectCase<string, string>(
                s => "matched-a",
                s => "matched-b"
            )
        );

        // Assert
        Assert.Contains(result, r => r.newItem == "matched-a");
        Assert.Contains(result, r => r.newItem == "matched-b");
        Assert.Contains(result, r => r.newItem == null);
    }

    #endregion

    #region ForEachCase

    [Fact]
    public async Task ForEachCase_ExecutesPerCategory()
    {
        // Arrange
        var counts = new int[3];
        var items = ToParallelAsync(new[] { "a", "b", "a", "c" })
            .Cases(s => s == "a", s => s == "b");

        // Act
        await CollectAsync(
            items.ForEachCase(
                () => Interlocked.Increment(ref counts[0]),
                () => Interlocked.Increment(ref counts[1]),
                () => Interlocked.Increment(ref counts[2])
            )
        );

        // Assert
        Assert.Equal(2, counts[0]);
        Assert.Equal(1, counts[1]);
        Assert.Equal(1, counts[2]);
    }

    [Fact]
    public async Task ForEachCase_WithItem_ReceivesItem()
    {
        // Arrange
        var collected = new System.Collections.Concurrent.ConcurrentBag<string>();
        var items = ToParallelAsync(new[] { "x", "y" })
            .Cases(s => s == "x");

        // Act
        await CollectAsync(
            items.ForEachCase(
                s => collected.Add($"X:{s}"),
                s => collected.Add($"other:{s}")
            )
        );

        // Assert
        Assert.Contains("X:x", collected);
        Assert.Contains("other:y", collected);
    }

    #endregion

    #region UnCase

    [Fact]
    public async Task UnCase_ReturnsOriginalItems()
    {
        // Arrange
        var items = ToParallelAsync(new[] { "apple", "banana", "cherry" })
            .Cases(s => s.StartsWith("a"));

        // Act
        var result = await CollectAsync(items.UnCase());

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("apple", result);
        Assert.Contains("banana", result);
        Assert.Contains("cherry", result);
    }

    #endregion

    #region AllCases

    [Fact]
    public async Task AllCases_WithFilterTrue_ExcludesDefaults()
    {
        // Arrange
        var items = ToParallelAsync(new[] { "a", "b", "c" })
            .Cases(s => s == "a", s => s == "b")
            .SelectCase<string, string>(
                s => "A",
                s => "B"
            );

        // Act
        var result = await CollectAsync(items.AllCases(filter: true));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
    }

    [Fact]
    public async Task AllCases_WithFilterFalse_IncludesDefaults()
    {
        // Arrange
        var items = ToParallelAsync(new[] { "a", "b", "c" })
            .Cases(s => s == "a", s => s == "b")
            .SelectCase<string, string>(
                s => "A",
                s => "B"
            );

        // Act
        var result = await CollectAsync(items.AllCases(filter: false));

        // Assert
        Assert.Equal(3, result.Count);
    }

    #endregion
}
