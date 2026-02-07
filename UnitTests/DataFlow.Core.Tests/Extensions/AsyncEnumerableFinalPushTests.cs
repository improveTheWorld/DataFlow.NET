using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Final push tests for AsyncEnumerableExtensions to reach 50% coverage.
/// </summary>
public class AsyncEnumerableFinalPushTests
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

    #region ToLines additional tests

    [Fact]
    public async Task ToLines_MultipleSeparators_CreatesMultipleLines()
    {
        // Arrange
        var slices = ToAsync(new[] { "a", "|", "b", "c", "|", "d", "|" });

        // Act
        var result = await CollectAsync(slices.ToLines("|"));

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("bc", result[1]);
        Assert.Equal("d", result[2]);
    }

    [Fact]
    public async Task ToLines_ConsecutiveSeparators_CreatesEmptyLines()
    {
        // Arrange
        var slices = ToAsync(new[] { "|", "|", "a", "|" });

        // Act
        var result = await CollectAsync(slices.ToLines("|"));

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("", result[0]);  // First separator
        Assert.Equal("", result[1]);  // Second separator
        Assert.Equal("a", result[2]); // Third
    }

    #endregion

    #region Flatten additional tests

    [Fact]
    public async Task Flatten_AsyncOfAsync_EmptyOuter_ReturnsEmpty()
    {
        // Arrange
        var nested = ToAsync(Array.Empty<IAsyncEnumerable<int>>());

        // Act
        var result = await CollectAsync(nested.Flatten());

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Flatten_AsyncOfAsync_EmptyInner_Skips()
    {
        // Arrange
        var nested = ToAsync(new[]
        {
            ToAsync(new[] { 1 }),
            ToAsync(Array.Empty<int>()),
            ToAsync(new[] { 2 })
        });

        // Act
        var result = await CollectAsync(nested.Flatten());

        // Assert
        Assert.Equal(new[] { 1, 2 }, result);
    }

    #endregion

    #region ForEach additional tests

    [Fact]
    public async Task ForEach_LargeSequence_ExecutesAll()
    {
        // Arrange
        var count = 0;
        var items = ToAsync(Enumerable.Range(1, 50));

        // Act - ForEach returns IAsyncEnumerable, need to consume it
        await CollectAsync(items.ForEach(_ => Interlocked.Increment(ref count)));

        // Assert
        Assert.Equal(50, count);
    }

    [Fact]
    public async Task ForEach_WithIndex_ReceivesCorrectIndices()
    {
        // Arrange
        var indices = new List<int>();
        var items = ToAsync(new[] { "a", "b", "c" });

        // Act - ForEach returns IAsyncEnumerable, need to consume it
        await CollectAsync(items.ForEach((_, idx) => indices.Add(idx)));

        // Assert
        Assert.Equal(new[] { 0, 1, 2 }, indices);
    }

    #endregion

    #region Until additional tests

    [Fact]
    public async Task Until_ImmediateStop_IncludesFirstItem()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act - Until is inclusive: yields the item that triggers the stop
        var result = await CollectAsync(items.Until(_ => true));

        // Assert - First item included (inclusive semantics, like do-while)
        Assert.Single(result);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public async Task Until_NeverStop_ReturnsAll()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items.Until(_ => false));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion
}
