using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for AsyncLinqOperators - Select, Where, SelectMany, Aggregate, Take, Skip, Distinct, Concat.
/// </summary>
public class AsyncLinqOperatorsTests
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

    #region Select

    [Fact]
    public async Task Select_TransformsElements()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items.Select(n => n * 10));

        // Assert
        Assert.Equal(new[] { 10, 20, 30 }, result);
    }

    [Fact]
    public async Task Select_WithIndex_PassesIndex()
    {
        // Arrange
        var items = ToAsync(new[] { "a", "b", "c" });

        // Act
        var result = await CollectAsync(items.Select((s, i) => $"{s}:{i}"));

        // Assert
        Assert.Equal(new[] { "a:0", "b:1", "c:2" }, result);
    }

    #endregion

    #region Where

    [Fact]
    public async Task Where_FiltersElements()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await CollectAsync(items.Where(n => n > 2));

        // Assert
        Assert.Equal(new[] { 3, 4, 5 }, result);
    }

    [Fact]
    public async Task Where_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items.Where(n => n > 100));

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region SelectMany

    [Fact]
    public async Task SelectMany_FlattensSequences()
    {
        // Arrange
        var items = ToAsync(new[] { "ab", "cde" });

        // Act - flatten each string into characters
        var result = await CollectAsync(items.SelectMany(s => s.ToCharArray()));

        // Assert
        Assert.Equal(new[] { 'a', 'b', 'c', 'd', 'e' }, result);
    }

    [Fact]
    public async Task SelectMany_WithIndex_PassesIndex()
    {
        // Arrange
        var items = ToAsync(new[] { "x", "y" });

        // Act
        var result = await CollectAsync(items.SelectMany((s, i) => new[] { $"{s}{i}", $"{s}{i + 10}" }));

        // Assert
        Assert.Equal(new[] { "x0", "x10", "y1", "y11" }, result);
    }

    #endregion

    #region Aggregate

    [Fact]
    public async Task Aggregate_WithSeed_AccumulatesValues()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3, 4 });

        // Act
        var result = await items.Aggregate(0, (acc, n) => acc + n);

        // Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public async Task Aggregate_Empty_ReturnsSeed()
    {
        // Arrange
        var items = ToAsync(Array.Empty<int>());

        // Act
        var result = await items.Aggregate(100, (acc, n) => acc + n);

        // Assert
        Assert.Equal(100, result); // seed is returned
    }

    #endregion

    #region Take and Skip

    [Fact]
    public async Task Take_LimitResults()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await CollectAsync(items.Take(3));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task Skip_SkipsElements()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await CollectAsync(items.Skip(2));

        // Assert
        Assert.Equal(new[] { 3, 4, 5 }, result);
    }

    #endregion

    #region Distinct and Concat

    [Fact]
    public async Task Distinct_RemovesDuplicates()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 2, 3, 3, 3 });

        // Act
        var result = await CollectAsync(items.Distinct());

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task Concat_CombinesSequences()
    {
        // Arrange
        var items1 = ToAsync(new[] { 1, 2 });
        var items2 = ToAsync(new[] { 3, 4 });

        // Act
        var result = await CollectAsync(items1.Concat(items2));

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4 }, result);
    }

    #endregion

    #region Empty and Async

    [Fact]
    public async Task Async_ConvertsEnumerableToAsync()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };

        // Act
        var result = await CollectAsync(items.Async());

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion
}
