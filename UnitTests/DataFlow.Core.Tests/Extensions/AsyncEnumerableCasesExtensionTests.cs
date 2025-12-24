using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for AsyncEnumerableCasesExtensions - async Cases pattern.
/// </summary>
public class AsyncEnumerableCasesExtensionTests
{
    #region Async Helpers

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
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
        var items = ToAsync(new[] { "error", "info", "warn", "error" });

        // Act
        var result = await CollectAsync(
            items.Cases(s => s == "error", s => s == "warn")
        );

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(0, result[0].category); // error -> 0
        Assert.Equal(2, result[1].category); // info -> supra (2)
        Assert.Equal(1, result[2].category); // warn -> 1
        Assert.Equal(0, result[3].category); // error -> 0
    }

    [Fact]
    public async Task Cases_WithCategoryEnum_MapsCorrectly()
    {
        // Arrange
        var items = ToAsync(new[]
        {
            ("A", 1),
            ("B", 2),
            ("A", 3)
        });

        // Act
        var result = await CollectAsync(items.Cases("A", "B"));

        // Assert
        Assert.Equal(0, result[0].categoryIndex); // A -> 0
        Assert.Equal(1, result[1].categoryIndex); // B -> 1
        Assert.Equal(0, result[2].categoryIndex); // A -> 0
    }

    #endregion

    #region SelectCase

    [Fact]
    public async Task SelectCase_TransformsPerCategory()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 })
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
        Assert.Equal(10, result[0].newItem);  // 1 * 10
        Assert.Equal(40, result[1].newItem);  // 2 * 20
        Assert.Equal(90, result[2].newItem);  // 3 * 30
    }

    [Fact]
    public async Task SelectCase_WithIndex_PassesIndex()
    {
        // Arrange
        var items = ToAsync(new[] { "a", "b", "c" })
            .Cases(s => true); // all category 0

        // Act
        var result = await CollectAsync(
            items.SelectCase((s, idx) => $"{s}:{idx}")
        );

        // Assert
        Assert.Equal("a:0", result[0].item);
        Assert.Equal("b:1", result[1].item);
        Assert.Equal("c:2", result[2].item);
    }

    #endregion

    #region ForEachCase

    [Fact]
    public async Task ForEachCase_ExecutesPerCategory()
    {
        // Arrange
        var collected = new List<string>();
        var items = ToAsync(new[] { "a", "b", "a" })
            .Cases(s => s == "a");

        // Act
        await CollectAsync(
            items.ForEachCase(
                s => collected.Add($"A:{s}"),
                s => collected.Add($"other:{s}")
            )
        );

        // Assert
        Assert.Contains("A:a", collected);
        Assert.Contains("other:b", collected);
        Assert.Equal(3, collected.Count);
    }

    #endregion

    #region AllCases and UnCase

    [Fact]
    public async Task AllCases_ExtractsTransformedValues()
    {
        // Arrange - use strings (reference type) since AllCases has 'where T : class'
        var items = ToAsync(new[] { "small", "large", "xtra" })
            .Cases(s => s.Length > 4) // small=5 is first match (>4), large=5 is also >4
            .SelectCase(
                s => $"match:{s}",
                s => $"no:{s}"
            );

        // Act
        var result = await CollectAsync(items.AllCases());

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task UnCase_ReturnsOriginalItems()
    {
        // Arrange
        var items = ToAsync(new[] { "x", "y", "z" })
            .Cases(s => s == "x");

        // Act
        var result = await CollectAsync(items.UnCase());

        // Assert
        Assert.Equal(new[] { "x", "y", "z" }, result);
    }

    #endregion
}
