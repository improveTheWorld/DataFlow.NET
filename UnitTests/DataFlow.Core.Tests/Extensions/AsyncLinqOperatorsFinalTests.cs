using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Final push tests for AsyncLinqOperators - First, FirstOrDefault, Concat, Distinct
/// </summary>
public class AsyncLinqOperatorsFinalTests
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

    #region First and FirstOrDefault

    [Fact]
    public async Task First_ReturnsFirst()
    {
        // Arrange
        var items = ToAsync(new[] { 10, 20, 30 });

        // Act
        var result = await items.First();

        // Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public async Task First_WithPredicate_ReturnsFirstMatch()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await items.First(n => n > 2);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task FirstOrDefault_Empty_ReturnsDefault()
    {
        // Arrange
        var items = ToAsync(Array.Empty<int>());

        // Act
        var result = await items.FirstOrDefault();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task FirstOrDefault_WithPredicate_NoMatch_ReturnsDefault()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await items.FirstOrDefault(n => n > 100);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Concat and Distinct

    [Fact]
    public async Task Concat_Empty_ReturnsOther()
    {
        // Arrange
        var items1 = ToAsync(Array.Empty<int>());
        var items2 = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items1.Concat(items2));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task Concat_BothNonEmpty_CombinesAll()
    {
        // Arrange
        var items1 = ToAsync(new[] { 1, 2 });
        var items2 = ToAsync(new[] { 3, 4, 5 });

        // Act
        var result = await CollectAsync(items1.Concat(items2));

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
    }

    [Fact]
    public async Task Distinct_WithComparer_Works()
    {
        // Arrange
        var items = ToAsync(new[] { "A", "a", "B", "b" });

        // Act
        var result = await CollectAsync(items.Distinct(StringComparer.OrdinalIgnoreCase));

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Distinct_Empty_ReturnsEmpty()
    {
        // Arrange
        var items = ToAsync(Array.Empty<int>());

        // Act
        var result = await CollectAsync(items.Distinct());

        // Assert
        Assert.Empty(result);
    }

    #endregion

}
