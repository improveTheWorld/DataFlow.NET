using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// More tests for AsyncEnumerableDebuggingExtensions - SelectMany overloads.
/// </summary>
public class AsyncEnumerableDebuggingSelectManyTests
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

    #region SelectMany with result selector

    [Fact]
    public async Task SelectMany_WithResultSelector_CombinesElements()
    {
        // Arrange
        var items = ToAsync(new[] { "ab", "cd" });

        // Act
        var result = await CollectAsync(
            items.SelectMany(
                s => ToAsync(s.ToCharArray()),
                (str, ch) => $"{str}:{ch}"));

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains("ab:a", result);
        Assert.Contains("ab:b", result);
        Assert.Contains("cd:c", result);
        Assert.Contains("cd:d", result);
    }

    #endregion

    #region Aggregate with seed

    [Fact]
    public async Task Aggregate_BuildsAccumulator()
    {
        // Arrange
        var items = ToAsync(new[] { "a", "b", "c" });

        // Act
        var result = await items.Aggregate("start:", (acc, s) => acc + s);

        // Assert
        Assert.Equal("start:abc", result);
    }

    [Fact]
    public async Task Aggregate_Empty_ReturnsSeed()
    {
        // Arrange
        var items = ToAsync(Array.Empty<int>());

        // Act
        var result = await items.Aggregate(42, (acc, n) => acc + n);

        // Assert
        Assert.Equal(42, result);
    }

    #endregion

    #region Additional Distinct tests

    [Fact]
    public async Task Distinct_Integers_RemovesDuplicates()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 1, 2, 2, 3, 3 });

        // Act
        var result = await CollectAsync(items.Distinct());

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Distinct_AllUnique_ReturnsAll()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await CollectAsync(items.Distinct());

        // Assert
        Assert.Equal(5, result.Count);
    }

    #endregion

    #region Additional Concat tests

    [Fact]
    public async Task Concat_SecondEmpty_ReturnsFirst()
    {
        // Arrange
        var items1 = ToAsync(new[] { 1, 2, 3 });
        var items2 = ToAsync(Array.Empty<int>());

        // Act
        var result = await CollectAsync(items1.Concat(items2));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task Concat_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var items1 = ToAsync(Array.Empty<int>());
        var items2 = ToAsync(Array.Empty<int>());

        // Act
        var result = await CollectAsync(items1.Concat(items2));

        // Assert
        Assert.Empty(result);
    }

    #endregion
}
