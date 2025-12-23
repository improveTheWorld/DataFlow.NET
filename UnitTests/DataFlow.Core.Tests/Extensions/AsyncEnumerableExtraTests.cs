using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Extra tests for AsyncEnumerableExtensions to reach 50% coverage.
/// </summary>
public class AsyncEnumerableExtraTests
{
    #region Helpers

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

    #region Select variations

    [Fact]
    public async Task Select_Empty_ReturnsEmpty()
    {
        // Arrange
        var items = ToAsync(Array.Empty<int>());

        // Act
        var result = await CollectAsync(items.Select(n => n * 10));

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Select_WithIndex_Empty_ReturnsEmpty()
    {
        // Arrange
        var items = ToAsync(Array.Empty<string>());

        // Act
        var result = await CollectAsync(items.Select((s, i) => $"{s}:{i}"));

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Where variations

    [Fact]
    public async Task Where_AllMatch_ReturnsAll()
    {
        // Arrange
        var items = ToAsync(new[] { 2, 4, 6 });

        // Act
        var result = await CollectAsync(items.Where(n => n % 2 == 0));

        // Assert
        Assert.Equal(new[] { 2, 4, 6 }, result);
    }

    [Fact]
    public async Task Where_WithIndex_FiltersCorrectly()
    {
        // Arrange
        var items = ToAsync(new[] { 10, 20, 30, 40, 50 });

        // Act - only keep even-indexed items
        var result = await CollectAsync(items.Where((n, idx) => idx % 2 == 0));

        // Assert
        Assert.Equal(new[] { 10, 30, 50 }, result);
    }

    #endregion

    #region Take variations

    [Fact]
    public async Task Take_Range_ReturnsSlice()
    {
        // Arrange
        var items = ToAsync(new[] { 0, 1, 2, 3, 4, 5 });

        // Act
        var result = await CollectAsync(items.Take(1..4));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task Take_RangeEmpty_ReturnsEmpty()
    {
        // Arrange
        var items = ToAsync(new[] { 0, 1, 2 });

        // Act
        var result = await CollectAsync(items.Take(0..0));

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Skip variations

    [Fact]
    public async Task Skip_WithIndexPredicate_Works()
    {
        // Arrange
        var items = ToAsync(new[] { 10, 20, 30, 40, 50 });

        // Act - skip while index < 2
        var result = await CollectAsync(items.Skip((n, idx) => idx < 2));

        // Assert
        Assert.Equal(new[] { 30, 40, 50 }, result);
    }

    [Fact]
    public async Task Skip_WithIndexPredicate_SkipAll()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items.Skip((n, idx) => true));

        // Assert
        Assert.Empty(result);
    }

    #endregion
}
